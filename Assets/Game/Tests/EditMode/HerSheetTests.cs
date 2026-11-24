using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 095 (Kristian, 2026-11-23: "tab knappen eller escape viser alle stats som er relevant å vise. også når man
    /// hovrer spells", and "har vi scaling på spells?"): her sheet's lines from made-up values: every stat present in its group
    /// with its start value, the change since the run's start, the cap's word, the sprint's line only with the sprint on; a
    /// skill's panel: base + flat by its weight for a quick, a multi-shot, a heavy skill and Slide, the cooldown with its base
    /// when cut, only the lines that apply; what she took for a skill.
    /// </summary>
    public class HerSheetTests
    {
        static void Say(string line)
        {
            try { TestContext.WriteLine(line); }
            catch (NullReferenceException) { Console.WriteLine(line); }
        }

        static Upgrade Step(UpgradeKind kind, float amount, string skill = "", UpgradeTier tier = UpgradeTier.Common) =>
            new Upgrade { Kind = kind, Amount = amount, Skill = skill, Tiered = true, Tier = tier };

        /// <summary>Her at a run's start with the stats on, hurt to 72 of 100.</summary>
        static OfferContext Start(RunModifiers m = null) =>
            new OfferContext { Modifiers = m ?? StatsDpsModel.Start(new RunValues()), Health = 72, OwnMaxHealth = 100 };

        static SkillDefinition Named(string name) => SkillDefaults.Create().First(s => s.Name == name);

        static SheetStat Line(List<SheetStat> lines, string name)
        {
            foreach (SheetStat s in lines) if (s.Name == name) return s;
            Assert.Fail("no line " + name + " in\n" + string.Join("\n", lines));
            return default;
        }

        // ---------------- The stats ----------------

        static readonly string[] Expected =
        {
            "Damage", "Basic shot", "Attack speed", "Crit chance", "Crit damage", "Reach", "Damage dealt", "Stagger",
            "Health", "Room heal", "Run speed", "Sprint speed", "Dashes", "Dash cooldown",
            "Cooldowns", "Clean press", "Marked prey", "Wide window", "Blast", "Marks last", "Focus gained", "Focus window",
            "Luck", "Rare or better",
        };

        [Test]
        public void AtARunsStart_EveryStatIsThere_InItsGroup_WithItsStartValue_NoneChanged_NoneAtItsCap()
        {
            List<SheetStat> lines = HerSheet.Stats(Start());
            foreach (SheetStat s in lines) Say(s.Group + ": " + s);
            Assert.That(lines.Select(s => s.Name), Is.EqualTo(Expected), "every stat that matters, in the groups' order");
            Assert.That(lines.Select(s => s.Group).Distinct(), Is.EqualTo(HerSheet.Order), "the four groups, in order");
            Assert.That(lines.Any(s => s.Changed), Is.False, "nothing changed at the start");
            Assert.That(lines.Any(s => s.AtCap), Is.False, "nothing at its cap at the start");
            Assert.That(Line(lines, "Damage").Value, Is.EqualTo("+0"));
            Assert.That(Line(lines, "Basic shot").Value, Is.EqualTo(Placeholders.BasicShotDamage.ToString()));
            Assert.That(Line(lines, "Attack speed").Value, Is.EqualTo("3.8 shots/s"), "0.85 of the feel's 0.225 s");
            Assert.That(Line(lines, "Crit chance").Value, Is.EqualTo("5 %"));
            Assert.That(Line(lines, "Crit damage").Value, Is.EqualTo("+50 %"));
            Assert.That(Line(lines, "Reach").Value, Is.EqualTo("10 m"));
            Assert.That(Line(lines, "Health").Value, Is.EqualTo("72 / 100"), "now and most");
            Assert.That(Line(lines, "Room heal").Value, Is.EqualTo("8"), "8 % of 100");
            Assert.That(Line(lines, "Run speed").Value, Is.EqualTo("6.0 m/s"));
            Assert.That(Line(lines, "Sprint speed").Value, Is.EqualTo("7.8 m/s"));
            Assert.That(Line(lines, "Dash cooldown").Value, Is.EqualTo("10 s"));
            Assert.That(Line(lines, "Cooldowns").Value, Is.EqualTo("0 %"));
            Assert.That(Line(lines, "Marks last").Value, Is.EqualTo("6 s"));
            Assert.That(Line(lines, "Focus window").Value, Is.EqualTo("6 s"), "today's window");
            Assert.That(Line(lines, "Luck").Value, Is.EqualTo("0"));
            Assert.That(Line(lines, "Rare or better").Value, Is.EqualTo("25 %"), "22 + 3 of 100");
        }

        [Test]
        public void AfterSteps_TheChangeSinceTheStartShows_InItsUnit_AndAStatAtItsCapSaysSo()
        {
            RunModifiers m = StatsDpsModel.Start(new RunValues());
            m.Take(Step(UpgradeKind.FlatDamage, 3f));
            m.Take(Step(UpgradeKind.FlatDamage, 3f));
            m.Take(Step(UpgradeKind.AttackSpeed, 0.6f, tier: UpgradeTier.Epic)); // past the cap: 0.85 * 1.6 > 1.3
            m.Take(Step(UpgradeKind.CritChance, 0.10f));
            m.Take(Step(UpgradeKind.MaxHealth, 20f));
            m.Take(Step(UpgradeKind.Blast, 0.40f));
            m.Take(Step(UpgradeKind.DashCooldown, 0.5f)); // to the floor
            int before = m.Count;
            List<SheetStat> lines = HerSheet.Stats(Start(m));
            foreach (SheetStat s in lines) Say(s.ToString());
            Assert.That(m.Count, Is.EqualTo(before), "the steps put back");
            Assert.That(m.Taken[0].Kind, Is.EqualTo(UpgradeKind.FlatDamage), "in their order");
            Assert.That(m.Taken[before - 1].Kind, Is.EqualTo(UpgradeKind.DashCooldown));

            Assert.That((Line(lines, "Damage").Value, Line(lines, "Damage").Change), Is.EqualTo(("+6", "+6")));
            Assert.That((Line(lines, "Basic shot").Value, Line(lines, "Basic shot").Change), Is.EqualTo(((Placeholders.BasicShotDamage + 6).ToString(), "+6")));
            SheetStat rate = Line(lines, "Attack speed");
            Assert.That((rate.Value, rate.Change, rate.AtCap), Is.EqualTo(("5.8 shots/s", "+2.0", true)), "1.3 of the feel's rate, its cap");
            Assert.That((Line(lines, "Crit chance").Value, Line(lines, "Crit chance").Change), Is.EqualTo(("15 %", "+10 %")));
            Assert.That((Line(lines, "Health").Value, Line(lines, "Health").Change), Is.EqualTo(("72 / 120", "+20")), "the most's change");
            SheetStat blast = Line(lines, "Blast");
            Assert.That((blast.Value, blast.Change, blast.AtCap), Is.EqualTo(("+40 %", "+40 %", true)));
            SheetStat dash = Line(lines, "Dash cooldown");
            Assert.That((dash.Value, dash.Change, dash.AtCap), Is.EqualTo(("5 s", "-5 s", true)), "at the cooldowns' floor");
            Assert.That(Line(lines, "Crit damage").Changed, Is.False, "a stat without steps unchanged");
            Assert.That(Line(lines, "Attack speed").ToString(), Does.EndWith(HerSheet.CapWord), "the cap's word");
        }

        [Test]
        public void TheSprintsLine_OnlyWithTheSprintOn_AndTheLoadoutLine_InAttack()
        {
            OfferContext c = Start();
            c.Feel = new FeelValues { Sprint = false };
            List<SheetStat> lines = HerSheet.Stats(c, "Pistols: Laser Sight");
            Assert.That(lines.Any(s => s.Name == "Sprint speed"), Is.False, "no sprint, no line");
            SheetStat loadout = Line(lines, "Loadout");
            Assert.That((loadout.Group, loadout.Value), Is.EqualTo((SheetGroup.Attack, "Pistols: Laser Sight")));
        }

        [Test]
        public void ThePauseMenusPanel_ReadsTheSameLines()
        {
            RunModifiers m = StatsDpsModel.Start(new RunValues());
            m.Take(Step(UpgradeKind.CritChance, 0.05f));
            foreach (SheetStat s in HerSheet.Stats(Start(m)))
            {
                MenuStat ms = HerSheet.AsMenuStat(s);
                Assert.That((ms.Name, ms.Value, ms.Change, ms.Changed), Is.EqualTo((s.Name, s.Value, s.Change, s.Changed)));
            }
        }

        /// <summary>
        /// Work order 099: the ranks bought between runs show their raised values as part of her start, as the rack's stat pieces
        /// do (no change since the run's start); with the rack's Thick Hide too, the shares add.
        /// </summary>
        [Test]
        public void TheRanksBoughtBetweenRuns_ShowTheirRaisedValues_AsPartOfHerStart()
        {
            var v = new MetaValues();
            var s = new MetaState { Currency = 10000 };
            for (int k = 0; k < 3; k++) MetaRanks.Buy(s, v, MetaRanks.Health);
            for (int k = 0; k < 2; k++) MetaRanks.Buy(s, v, MetaRanks.Damage);
            for (int k = 0; k < 2; k++) MetaRanks.Buy(s, v, MetaRanks.CritChance);
            for (int k = 0; k < 2; k++) MetaRanks.Buy(s, v, MetaRanks.RoomHeal);
            RunModifiers m = StatsDpsModel.Start(new RunValues());
            MetaRanks.Apply(s, v, m.Meta);
            List<SheetStat> lines = HerSheet.Stats(Start(m));
            foreach (SheetStat l in lines) Say(l.ToString());
            Assert.That(Line(lines, "Health").Value, Is.EqualTo("72 / 130"), "+30 % of her own 100");
            Assert.That(Line(lines, "Room heal").Value, Is.EqualTo("16"), "12 % of 130 = 15.6");
            Assert.That(Line(lines, "Crit chance").Value, Is.EqualTo("11 %"), "5 % and 6 points");
            Assert.That(Line(lines, "Damage dealt").Value, Is.EqualTo("+12 %"));
            Assert.That(Line(lines, "Basic shot").Value, Is.EqualTo(m.ScaleDamage(Placeholders.BasicShotDamage).ToString()));
            Assert.That(m.ScaleDamage(Placeholders.BasicShotDamage), Is.GreaterThan(Placeholders.BasicShotDamage), "raised");
            Assert.That(lines.Any(l => l.Changed), Is.False, "part of her start, as the rack's pieces are");

            s.Owned.Add(MetaPieces.ThickHide);
            s.Equipped.Add(MetaPieces.ThickHide);
            RunModifiers both = StatsDpsModel.Start(new RunValues());
            MetaRack.Apply(s, v, both.Meta);
            MetaRanks.Apply(s, v, both.Meta);
            Assert.That(Line(HerSheet.Stats(Start(both)), "Health").Value, Is.EqualTo("72 / 150"), "Thick Hide's 20 % and the ranks' 30 %");
        }

        // ---------------- A skill's panel ----------------

        static OfferContext WithFlat(float flat, params Upgrade[] more)
        {
            RunModifiers m = StatsDpsModel.Start(new RunValues());
            if (flat > 0f) m.Take(Step(UpgradeKind.FlatDamage, flat));
            foreach (Upgrade u in more) m.Take(u);
            return Start(m);
        }

        [Test]
        public void AQuickSkill_TakesYourFlatDamageOnce_SaidPlainly()
        {
            SheetSkill p = HerSheet.Skill(Named("Quick Shot"), "Q", WithFlat(6f));
            Say(p.ToString());
            Assert.That(p.ValueOf("damage"), Is.EqualTo("106   (100 base + 6 flat)"));
            Assert.That(p.ValueOf("your damage"), Is.EqualTo("takes 1 × your +6 damage: +6"));
            Assert.That(p.Sentence, Is.EqualTo(OfferTexts.SkillSentence(Named("Quick Shot"))), "the card's words");
        }

        [Test]
        public void AMultiShotSkill_TakesHalf_EachShot()
        {
            SheetSkill p = HerSheet.Skill(Named("Triple Tap"), "RMB", WithFlat(6f));
            Say(p.ToString());
            Assert.That(p.ValueOf("damage"), Is.EqualTo("73 a shot   (70 base + 3 flat)"));
            Assert.That(p.ValueOf("your damage"), Is.EqualTo("takes 0.5 × your +6 damage: +3"));
            Assert.That(p.ValueOf("shots"), Is.EqualTo("3 taps, a shot each"));
            Assert.That(p.ValueOf("window"), Does.StartWith("the next press within 0.6 s, its clean part"));
            Assert.That(p.ValueOf("crit"), Does.Contain("a clean last press always crits"));
        }

        [Test]
        public void AHeavySkill_TakesTwice()
        {
            SheetSkill p = HerSheet.Skill(Named("Scatter Blast"), "R", WithFlat(6f));
            Say(p.ToString());
            Assert.That(p.ValueOf("damage"), Is.EqualTo("232   (220 base + 12 flat)"));
            Assert.That(p.ValueOf("your damage"), Is.EqualTo("takes 2 × your +6 damage: +12"));
            Assert.That(p.ValueOf("reach"), Is.EqualTo("5 m, a cone 70°"));
            Assert.That(p.ValueOf("marks"), Is.EqualTo("spends every mark: +25 % damage a mark"));
        }

        [Test]
        public void Slide_TakesNone_AndShowsNoDamageNorCrit()
        {
            SheetSkill p = HerSheet.Skill(Named("Slide"), "4", WithFlat(6f));
            Say(p.ToString());
            Assert.That(p.ValueOf("your damage"), Is.EqualTo("takes none of your damage"));
            Assert.That(p.ValueOf("damage"), Is.Null, "it deals none");
            Assert.That(p.ValueOf("crit"), Is.Null);
            Assert.That(p.ValueOf("slide"), Is.EqualTo("6 m the way you move"));
            Assert.That(p.ValueOf("shots"), Is.EqualTo("2 charges"));
        }

        [Test]
        public void TheCooldown_IsNow_AndItsBaseWhenCut()
        {
            Assert.That(HerSheet.Skill(Named("Scatter Blast"), "R", WithFlat(0f)).ValueOf("cooldown"), Is.EqualTo("6 s"), "not cut: no base");
            SheetSkill cut = HerSheet.Skill(Named("Scatter Blast"), "R", WithFlat(0f, Step(UpgradeKind.Cooldown, 0.05f, "Scatter Blast")));
            Assert.That(cut.ValueOf("cooldown"), Is.EqualTo("5.7 s  (base 6 s)"));
            SheetSkill all = HerSheet.Skill(Named("Quick Shot"), "Q", WithFlat(0f, Step(UpgradeKind.AllCooldowns, 0.1f)));
            Assert.That(all.ValueOf("cooldown"), Is.EqualTo("2.7 s  (base 3 s)"), "every skill's cut too");
        }

        [Test]
        public void OnlyTheLinesThatApply()
        {
            OfferContext c = WithFlat(0f);
            string Labels(string name) => string.Join(",", HerSheet.Skill(Named(name), "", c).Lines.Select(l => l.Label));
            Assert.That(Labels("Quick Shot"), Is.EqualTo("damage,your damage,cooldown,reach,marks,a mark,crit,stagger"));
            Assert.That(Labels("Long Shot"), Does.Contain("charge").And.Not.Contain("window").And.Not.Contain("marks"));
            Assert.That(Labels("Heavy Volley"), Does.Contain("cast"));
            Assert.That(Labels("Powder Keg"), Does.Contain("fuse").And.Not.Contain("shots"));
            Assert.That(Labels("Tag Round"), Does.Contain("tag"));
            Assert.That(Labels("Stopping Shot"), Does.Contain("counter"));
            Assert.That(HerSheet.Skill(Named("Powder Keg"), "1", c).ValueOf("reach"), Is.EqualTo("thrown up to 8 m, a burst 2.5 m round"));
        }

        [Test]
        public void TheBasicShot_TakesItOnce_WithItsRateAndReach()
        {
            SheetSkill p = HerSheet.Skill(null, "LMB", WithFlat(6f));
            Say(p.ToString());
            Assert.That(p.Name, Is.EqualTo(HerSheet.BasicShotName));
            Assert.That(p.ValueOf("your damage"), Is.EqualTo("takes 1 × your +6 damage: +6"));
            Assert.That(p.ValueOf("damage"), Is.EqualTo((Placeholders.BasicShotDamage + 6) + "   (" + Placeholders.BasicShotDamage + " base + 6 flat)"));
            Assert.That(p.ValueOf("fire rate"), Does.StartWith("3.8 shots/s"));
            Assert.That(p.ValueOf("reach"), Is.EqualTo("10 m"));
        }

        [Test]
        public void BlastAndWideWindow_AreSaidWhereTheyApply()
        {
            OfferContext c = WithFlat(0f, Step(UpgradeKind.Blast, 0.2f), Step(UpgradeKind.WideWindow, 0.1f));
            SkillStatLayer layer = c.Stats.SkillLayer;
            SheetSkill keg = HerSheet.Skill(layer.Apply(Named("Powder Keg")), "1", c);
            Assert.That(keg.ValueOf("reach"), Is.EqualTo("thrown up to 8 m, a burst 3 m round  (+20 % blast)"), "the skill as she has it, Blast laid on");
            SheetSkill tap = HerSheet.Skill(Named("Triple Tap"), "RMB", c);
            Assert.That(tap.ValueOf("window"), Does.EndWith("(+10 % wide window)"));
            Assert.That(HerSheet.Skill(Named("Quick Shot"), "Q", c).ValueOf("reach"), Is.EqualTo("12 m"), "no area, no blast");
        }

        [Test]
        public void WhatSheTookForASkill_IsListed_ByTheCardsName()
        {
            RunModifiers m = StatsDpsModel.Start(new RunValues());
            m.Take(Step(UpgradeKind.Cooldown, 0.05f, "Scatter Blast"));
            m.Take(Step(UpgradeKind.Cooldown, 0.10f, "Quick Shot", UpgradeTier.Rare));
            m.Take(Step(UpgradeKind.AttackSpeed, 0.06f));
            Assert.That(HerSheet.TakenFor("Scatter Blast", m, new[] { "Point Blank" }), Is.EqualTo(new[] { "Point Blank", "Cooldown -5 %" }));
            Assert.That(HerSheet.TakenFor("Quick Shot", m), Is.EqualTo(new[] { "Cooldown -10 % (rare)" }));
            Assert.That(HerSheet.TakenFor(null, m), Is.EqualTo(new[] { "Attack speed +6 %" }), "the basic shot's: her rate");
        }
    }
}
