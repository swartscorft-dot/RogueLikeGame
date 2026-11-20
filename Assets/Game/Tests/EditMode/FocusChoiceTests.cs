using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 025 (2026-11-20): the focus choice as rules. Hair Trigger's three-part gauge and its priming, Steady
    /// Hand's window and the two skills it changes, the choice offered once after floor 1's reward; and the chests' five
    /// new rows, their limits, and the run's modifiers cleared.
    /// </summary>
    public class FocusChoiceTests
    {
        static readonly IdentityValues Identity = new IdentityValues(); // 100 focus
        static readonly FocusModeValues Ways = new FocusModeValues();

        static SkillDefinition Skill(string name) => SkillDefaults.Create().First(s => s.Name == name);

        // ---------------- Hair Trigger: the gauge in three parts ----------------

        [Test]
        public void TheGauge_InThreeParts_FillsPartByPart_AndHoldsThree()
        {
            var gauge = new FocusGauge();
            Assert.That(FocusGauge.PartSize(Identity, 3), Is.EqualTo(100f / 3f).Within(1e-4f));
            Assert.That(gauge.PartsFull(Identity, 3), Is.EqualTo(0));
            gauge.Add(30f, Identity, 0f);
            Assert.That(gauge.PartsFull(Identity, 3), Is.EqualTo(0), "30 of a 33.3 part");
            gauge.Add(5f, Identity, 0f);
            Assert.That(gauge.PartsFull(Identity, 3), Is.EqualTo(1));
            gauge.Add(40f, Identity, 0f);
            Assert.That(gauge.PartsFull(Identity, 3), Is.EqualTo(2));
            gauge.Add(500f, Identity, 0f);
            Assert.That(gauge.PartsFull(Identity, 3), Is.EqualTo(3), "she may hold three");
            Assert.That(gauge.Focus, Is.EqualTo(100f), "and no more than a full gauge");
        }

        [Test]
        public void SpendingAPart_TakesOnePart_KeepsWhatFillsTheNext_AndOpensNoWindow()
        {
            var gauge = new FocusGauge();
            Assert.That(gauge.TrySpendPart(Identity, 3), Is.False, "none full");
            gauge.Add(80f, Identity, 0f); // two parts and 13.3 more
            Assert.That(gauge.TrySpendPart(Identity, 3), Is.True);
            Assert.That(gauge.Focus, Is.EqualTo(80f - 100f / 3f).Within(1e-3f));
            Assert.That(gauge.PartsFull(Identity, 3), Is.EqualTo(1));
            Assert.That(gauge.IsEmpowered(0f), Is.False, "no window: basic shots do not mark");
            Assert.That(gauge.Add(5f, Identity, 0.1f), Is.EqualTo(5f), "and focus keeps filling at once");
            Assert.That(gauge.TrySpendPart(Identity, 3), Is.True);
            Assert.That(gauge.TrySpendPart(Identity, 3), Is.False, "one part at a time, while there is one");

            gauge.Fill(Identity);
            for (int i = 0; i < 3; i++) Assert.That(gauge.TrySpendPart(Identity, 3), Is.True, "three held, three spent");
            Assert.That(gauge.TrySpendPart(Identity, 3), Is.False);
        }

        [Test]
        public void APart_CutsEveryRunningCooldown_ByItsShareOfWhatIsLeft()
        {
            Assert.That(Ways.PartCooldownCut, Is.EqualTo(0.15f));
            var cooldown = new Cooldown();
            cooldown.Start(0f, 10f);
            float cut = cooldown.Cut(2f, Ways.PartCooldownCut); // 8 s left
            Assert.That(cut, Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(cooldown.Remaining(2f), Is.EqualTo(6.8f).Within(1e-4f));
            Assert.That(cooldown.Cut(2f, Ways.PartCooldownCut), Is.EqualTo(6.8f * 0.15f).Within(1e-4f), "a second part, of what is left then");
        }

        // ---------------- Priming ----------------

        [Test]
        public void Priming_LastsFourSeconds_IsSpentByOneSkill_AndLapsesOnce()
        {
            var prime = new FocusPrime();
            Assert.That(prime.IsPrimed(0f), Is.False);
            prime.Prime(1f, Ways.PrimeSeconds);
            Assert.That(prime.IsPrimed(4.9f), Is.True);
            Assert.That(prime.Spend(2f), Is.True, "the skill that uses it");
            Assert.That(prime.Spend(2.1f), Is.False, "one skill spends it");
            Assert.That(prime.TakeLapsed(10f), Is.False, "spent, it does not lapse");

            prime.Prime(20f, Ways.PrimeSeconds);
            Assert.That(prime.TakeLapsed(23.9f), Is.False);
            Assert.That(prime.IsPrimed(24.01f), Is.False);
            Assert.That(prime.TakeLapsed(24.01f), Is.True, "it lapsed");
            Assert.That(prime.TakeLapsed(25f), Is.False, "once");
            Assert.That(prime.Spend(24.5f), Is.False);

            prime.Prime(30f, Ways.PrimeSeconds);
            prime.Prime(33f, Ways.PrimeSeconds);
            Assert.That(prime.IsPrimed(36.5f), Is.True, "a second part starts it again");
            prime.Clear();
            Assert.That(prime.IsPrimed(36.5f), Is.False);
            Assert.That(prime.TakeLapsed(40f), Is.False, "cleared, nothing lapses");
        }

        [Test]
        public void WhatPrimingGivesEachOfTheTwelve()
        {
            var want = new Dictionary<string, PrimeUse>
            {
                { "Quick Shot", PrimeUse.ExtraMark }, { "Powder Keg", PrimeUse.ExtraMark }, { "Fan Fire", PrimeUse.ExtraMark },
                { "Long Shot", PrimeUse.PerfectRelease }, { "Scatter Blast", PrimeUse.SpenderExtraMark },
                { "Heavy Volley", PrimeUse.SpenderExtraMark }, { "Backstep", PrimeUse.CleanPress }, { "Triple Tap", PrimeUse.CleanPress },
                { "Tag Round", PrimeUse.ExtraMark }, { "Breaker Slug", PrimeUse.SpenderExtraMark }, { "Slide", PrimeUse.None },
                { "Stopping Shot", PrimeUse.ExtraMark },
            };
            SkillDefinition[] all = SkillDefaults.Create();
            Assert.That(all.Select(s => s.Name), Is.EquivalentTo(want.Keys));
            foreach (SkillDefinition s in all) Assert.That(FocusModeRules.UseFor(s), Is.EqualTo(want[s.Name]), s.Name);

            // Snap Shot (work order 017) is a perfect release already: it leaves the priming for the next skill.
            SkillDefinition snap = RunSkillUpgrades.Patched(Skill("Long Shot"), new[] { SkillUpgradeTable.Find(SkillUpgradeTable.Create(), "Long Shot", "Snap Shot") });
            Assert.That(FocusModeRules.UseFor(snap), Is.EqualTo(PrimeUse.None));
            // Slug makes Scatter Blast a line: still a spender.
            SkillDefinition slug = RunSkillUpgrades.Patched(Skill("Scatter Blast"), new[] { SkillUpgradeTable.Find(SkillUpgradeTable.Create(), "Scatter Blast", "Slug") });
            Assert.That(FocusModeRules.UseFor(slug), Is.EqualTo(PrimeUse.SpenderExtraMark));
        }

        [Test]
        public void APrimedChain_ItsLastPress_IsCleanAnywhereInTheWindow_AndOnlyTheLast()
        {
            var chain = new RecastChain();
            SkillDefinition tap = Skill("Triple Tap");
            chain.Start(7, tap.RecastCount, tap.RecastWindow);
            chain.ShotWentOff(0.15f);
            RecastPress second = chain.Judge(7, 0.2f, 1f / 3f, true);
            Assert.That(FocusModeRules.Primed(second, chain.NextIsLast, true), Is.EqualTo(RecastPress.Shot), "the second press is no last press");
            chain.Recast();
            chain.ShotWentOff(0.35f);
            RecastPress early = chain.Judge(7, 0.36f, 1f / 3f, true); // at once in the window: far from its clean part
            Assert.That(early, Is.EqualTo(RecastPress.Shot), "unprimed, an ordinary shot");
            Assert.That(FocusModeRules.Primed(early, chain.NextIsLast, true), Is.EqualTo(RecastPress.Clean), "primed, clean");
            Assert.That(FocusModeRules.Primed(early, chain.NextIsLast, false), Is.EqualTo(RecastPress.Shot), "a skill with no clean press stays ordinary");
            Assert.That(FocusModeRules.Primed(RecastPress.Early, true, true), Is.EqualTo(RecastPress.Early), "before the window it still waits");
            Assert.That(FocusModeRules.Primed(RecastPress.None, true, true), Is.EqualTo(RecastPress.None));
        }

        [Test]
        public void APrimedRelease_IsPerfectFromHalfACharge_AndAPrimedSpender_CountsAMarkMore_UpToTheCap()
        {
            Assert.That(FocusModeRules.PrimedPerfect(0.5f, Ways), Is.True);
            Assert.That(FocusModeRules.PrimedPerfect(0.49f, Ways), Is.False);
            Assert.That(FocusModeRules.PrimedPerfect(1f, Ways), Is.True, "and past the band too");
            Assert.That(PoolRules.IsPerfect(Skill("Long Shot"), 0.5f), Is.False, "unprimed, half a charge is not perfect");

            Assert.That(FocusModeRules.SpenderCount(0, 1, 5), Is.EqualTo(1), "one mark more than none");
            Assert.That(FocusModeRules.SpenderCount(3, 1, 5), Is.EqualTo(4));
            Assert.That(FocusModeRules.SpenderCount(5, 1, 5), Is.EqualTo(5), "up to the cap");
            SkillDefinition blast = Skill("Scatter Blast");
            Assert.That(MarkRules.SpenderDamage(blast.Damage, FocusModeRules.SpenderCount(0, 1, 5), blast.MarkDamageBonus), Is.EqualTo(275),
                        "a primed blast on a target with no marks deals one mark's bonus");
        }

        // ---------------- Steady Hand ----------------

        [Test]
        public void SteadyHand_AFullGauge_OpensATenSecondWindow_DrawnRunningDown()
        {
            var gauge = new FocusGauge();
            gauge.Add(99f, Identity, 0f);
            Assert.That(gauge.TryActivate(0f, Identity, Ways.WindowSeconds), Is.False, "only a full gauge");
            gauge.Fill(Identity);
            Assert.That(gauge.TryActivate(1f, Identity, Ways.WindowSeconds), Is.True);
            Assert.That(gauge.Focus, Is.EqualTo(0f));
            Assert.That(gauge.IsEmpowered(10.99f), Is.True);
            Assert.That(gauge.IsEmpowered(11.01f), Is.False, "ten seconds, not six");
            Assert.That(gauge.BarFill(6f, Identity), Is.EqualTo(0.5f).Within(1e-4f), "half of its own length left");
            Assert.That(gauge.Add(10f, Identity, 5f), Is.EqualTo(0f), "no focus is gained in the window, as today");

            var today = new FocusGauge();
            today.Fill(Identity);
            Assert.That(today.TryActivate(0f, Identity), Is.True);
            Assert.That(today.EmpoweredRemaining(0f), Is.EqualTo(Identity.EmpoweredDuration), "today's is as it was");
        }

        [Test]
        public void SteadyHand_TripleTap_TakesFivePresses_AndScatterBlast_FiresTwice_AQuarterSecondApart()
        {
            SkillDefinition tap = Skill("Triple Tap"), blast = Skill("Scatter Blast"), quick = Skill("Quick Shot");
            SkillDefinition tap5 = FocusModeRules.InWindow(tap, Ways), blast2 = FocusModeRules.InWindow(blast, Ways);
            Assert.That(tap5.RecastCount, Is.EqualTo(5), "five presses");
            Assert.That(tap.RecastCount, Is.EqualTo(3), "the skill it was copied from is not written");
            Assert.That(tap5.Cooldown, Is.EqualTo(tap.Cooldown), "nothing else changes");
            Assert.That(blast2.HitCount, Is.EqualTo(2));
            Assert.That(blast2.HitInterval, Is.EqualTo(0.25f));
            Assert.That(blast2.Cooldown, Is.EqualTo(blast.Cooldown), "no extra cooldown");
            Assert.That(blast.HitCount, Is.EqualTo(1));
            Assert.That(FocusModeRules.InWindow(quick, Ways), Is.SameAs(quick), "every other skill as it is");

            // The fifth press is the clean one.
            var chain = new RecastChain();
            chain.Start(7, tap5.RecastCount, tap5.RecastWindow);
            float t = 0f;
            for (int press = 2; press <= 5; press++)
            {
                chain.ShotWentOff(t);
                Assert.That(chain.NextIsLast, Is.EqualTo(press == 5), "press " + press);
                Assert.That(chain.Judge(7, t + tap5.RecastWindow * 0.9f, 1f / 3f, true), Is.EqualTo(press == 5 ? RecastPress.Clean : RecastPress.Shot));
                chain.Recast();
                t += 0.5f;
            }
            Assert.That(chain.IsComplete, Is.True);

            // Two blasts: the second 0.25 s after the first, the same use.
            var cast = new SkillCast();
            cast.Start(blast2, 0f);
            Assert.That(cast.TakeHits(blast.Startup + 0.01f), Is.EqualTo(1));
            Assert.That(cast.TakeHits(blast.Startup + 0.24f), Is.EqualTo(0));
            Assert.That(cast.TakeHits(blast.Startup + 0.26f), Is.EqualTo(1), "the second blast");
            Assert.That(cast.TakeHits(5f), Is.EqualTo(0), "two and no more");
        }

        [Test]
        public void SteadyHand_AddsTwoPresses_ToWhateverTheRunsTripleTapHas()
        {
            SkillUpgradeDefinition[] table = SkillUpgradeTable.Create();
            SkillDefinition fourth = RunSkillUpgrades.Patched(Skill("Triple Tap"), new[] { SkillUpgradeTable.Find(table, "Triple Tap", "Fourth Tap") });
            Assert.That(fourth.RecastCount, Is.EqualTo(4));
            Assert.That(FocusModeRules.InWindow(fourth, Ways).RecastCount, Is.EqualTo(6), "Fourth Tap's four, and two");
            SkillDefinition slug = RunSkillUpgrades.Patched(Skill("Scatter Blast"), new[] { SkillUpgradeTable.Find(table, "Scatter Blast", "Slug") });
            SkillDefinition slug2 = FocusModeRules.InWindow(slug, Ways);
            Assert.That(slug2.Shape, Is.EqualTo(SkillShape.Line), "Slug's line");
            Assert.That(slug2.HitCount, Is.EqualTo(2), "fired twice");
        }

        [Test]
        public void TheWindowsView_IsCopies_MadeAgainOnlyWhenTheListChanges()
        {
            SkillDefinition[] skills = SkillDefaults.Create();
            var view = new SteadyHandView();
            IReadOnlyList<SkillDefinition> a = view.View(skills, Ways);
            Assert.That(view.View(skills, Ways), Is.SameAs(a));
            Assert.That(a[7].RecastCount, Is.EqualTo(5));
            Assert.That(skills[7].RecastCount, Is.EqualTo(3), "never written back");
            Assert.That(a[0], Is.SameAs(skills[0]));
            Assert.That(view.View(SkillDefaults.Create(), Ways), Is.Not.SameAs(a));
        }

        // ---------------- The choice ----------------

        [Test]
        public void TheChoice_IsOfferedAfterFloor1Only_TheSameTwoInTheSameOrder_WithThreeLinesEach()
        {
            Assert.That(FocusModeRules.OfferedAfter(1, Ways), Is.True);
            for (int floor = 2; floor <= 5; floor++) Assert.That(FocusModeRules.OfferedAfter(floor, Ways), Is.False, "floor " + floor);
            Assert.That(FocusModeRules.Options, Is.EqualTo(new[] { FocusMode.HairTrigger, FocusMode.SteadyHand }), "nothing drawn: every run seed the same");
            foreach (FocusMode mode in FocusModeRules.Options)
            {
                string[] lines = FocusModeRules.Lines(mode, Ways, 0.4f);
                Assert.That(lines.Length, Is.EqualTo(3));
                Assert.That(lines.All(l => l.Length > 0 && l.Length <= 90), Is.True, string.Join(" / ", lines));
            }
            StringAssert.Contains("3 parts", FocusModeRules.Lines(FocusMode.HairTrigger, Ways)[0]);
            StringAssert.Contains("15%", FocusModeRules.Lines(FocusMode.HairTrigger, Ways)[1]);
            StringAssert.Contains("10 s", FocusModeRules.Lines(FocusMode.SteadyHand, Ways)[1]);
            Assert.That(FocusModeRules.Name(FocusMode.HairTrigger), Is.EqualTo("Hair Trigger"));
            Assert.That(FocusModeRules.Name(FocusMode.SteadyHand), Is.EqualTo("Steady Hand"));
        }

        [Test]
        public void TheRunsChoice_IsTodaysUntilTaken_AndCleared()
        {
            var choice = new RunFocusChoice();
            Assert.That(choice.Mode, Is.EqualTo(FocusMode.Today));
            choice.Offer();
            Assert.That(choice.Offered && !choice.Taken, Is.True);
            Assert.That(choice.Mode, Is.EqualTo(FocusMode.Today), "offered, not yet taken: today's gauge");
            choice.Take(FocusMode.SteadyHand);
            Assert.That(choice.Mode, Is.EqualTo(FocusMode.SteadyHand));
            choice.Clear();
            Assert.That(choice.Mode, Is.EqualTo(FocusMode.Today));
            Assert.That(choice.Offered || choice.Taken, Is.False);
        }

        [Test]
        public void TheRecord_AndTheSummary_NameTheChoice()
        {
            var record = new RunRecord("ABCDEFGH", 5);
            Assert.That(record.FocusChoiceText, Is.EqualTo("not chosen"));
            record.TakeFocusChoice("Hair Trigger");
            Assert.That(record.FocusChoiceText, Is.EqualTo("Hair Trigger"));

            static SessionEvent E(double t, string type, string name = null, params string[] extra)
            {
                var e = new SessionEvent(t, type, name);
                for (int i = 0; i + 1 < extra.Length; i += 2) e.AddExtra(extra[i], extra[i + 1]);
                return e;
            }
            var events = new List<SessionEvent>
            {
                E(0, SessionEventTypes.SessionStart),
                E(0, SessionEventTypes.RunStart, "ABCDEFGH"),
                E(10, SessionEventTypes.FocusSpent, null, "as", "today"),
                E(20, SessionEventTypes.FocusChoiceOffer, null, "floor", "1", "offered", "Hair Trigger | Steady Hand"),
                E(21, SessionEventTypes.FocusChoiceTake, "Hair Trigger", "floor", "1"),
                E(30, SessionEventTypes.FocusSpent, null, "as", "part"),
                E(31, SessionEventTypes.PrimeUsed, "Triple Tap"),
                E(40, SessionEventTypes.FocusSpent, null, "as", "part"),
                E(45, SessionEventTypes.PrimeLapsed),
                E(60, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Runs.Single().FocusChoice, Is.EqualTo("Hair Trigger"));
            StringAssert.Contains("  focus choice     Hair Trigger   spent 2, primed skills used 1, lapsed 1", SessionSummary.Format(stats));
        }

        // ---------------- The chests' new rows ----------------

        static Upgrade Step(UpgradeKind kind, float amount, int heal = 0) => new Upgrade { Kind = kind, Amount = amount, Heal = heal };

        [Test]
        public void TheFiveNewRows_AreInTheTable_WithTheirNumbers()
        {
            UpgradeDefinition[] rows = new RunValues().Upgrades;
            Assert.That(rows.Length, Is.EqualTo(11));
            UpgradeDefinition Row(UpgradeKind k) => rows.Single(r => r.Kind == k);
            Assert.That(Row(UpgradeKind.Mend).Heal, Is.EqualTo(30));
            Assert.That(Row(UpgradeKind.LongerBarrel).Amount, Is.EqualTo(1f));
            Assert.That(Row(UpgradeKind.LongerBarrel).MaxTaken, Is.EqualTo(3));
            Assert.That(Row(UpgradeKind.LastingMarks).Amount, Is.EqualTo(2f));
            Assert.That(Row(UpgradeKind.KeenFocus).Amount, Is.EqualTo(0.1f));
            Assert.That(Row(UpgradeKind.HeavyHits).Amount, Is.EqualTo(0.1f));
        }

        [Test]
        public void EachNewRow_StacksInTheRunsModifiers_AndShowsInShortForm()
        {
            var m = new RunModifiers();
            m.Take(Step(UpgradeKind.Mend, 0f, 30));
            Assert.That(m.BonusMaxHealth, Is.EqualTo(0), "Mend does not raise her maximum");
            for (int i = 0; i < 2; i++)
            {
                m.Take(Step(UpgradeKind.LongerBarrel, 1f));
                m.Take(Step(UpgradeKind.LastingMarks, 2f));
                m.Take(Step(UpgradeKind.KeenFocus, 0.1f));
                m.Take(Step(UpgradeKind.HeavyHits, 0.1f));
            }
            Assert.That(m.BasicShotRangeBonus, Is.EqualTo(2f));
            Assert.That(m.MarkLifetimeBonus, Is.EqualTo(4f));
            Assert.That(m.FocusGainShare, Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(m.StaggerShare, Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(m.CountOf(UpgradeKind.LongerBarrel), Is.EqualTo(2));
            Assert.That(m.ShortText(), Is.EqualTo("heal 30, +1 m reach x2, +2 s marks x2, +10% focus x2, +10% stagger x2"));
            m.Clear();
            Assert.That(m.Count, Is.Zero);
            Assert.That(m.BasicShotRangeBonus + m.MarkLifetimeBonus, Is.EqualTo(0f), "cleared with the run");
            Assert.That(m.FocusGainShare, Is.EqualTo(1f));
            Assert.That(m.StaggerShare, Is.EqualTo(1f));
        }

        [Test]
        public void Mend_IsNeverOfferedAtFullHealth_AndLongerBarrel_AtMostThreeTimes()
        {
            var v = new RunValues();
            var equipped = new List<string> { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" };
            var three = new RunModifiers();
            for (int i = 0; i < 3; i++) three.Take(Step(UpgradeKind.LongerBarrel, 1f));
            var two = new RunModifiers();
            for (int i = 0; i < 2; i++) two.Take(Step(UpgradeKind.LongerBarrel, 1f));
            bool mendWhenHurt = false, barrelAtTwo = false;
            for (ulong run = 0; run < 400; run++)
            {
                List<Upgrade> full = ChestUpgrades.Offer(run, 1, 3, equipped, v, null, atFullHealth: true);
                Assert.That(full.Count, Is.EqualTo(2));
                Assert.That(full.Any(u => u.Kind == UpgradeKind.Mend), Is.False, "never at full health");
                mendWhenHurt |= ChestUpgrades.Offer(run, 1, 3, equipped, v, null, atFullHealth: false).Any(u => u.Kind == UpgradeKind.Mend);
                List<Upgrade> after3 = ChestUpgrades.Offer(run, 2, 4, equipped, v, three, false);
                Assert.That(after3.Count, Is.EqualTo(2));
                Assert.That(after3.Any(u => u.Kind == UpgradeKind.LongerBarrel), Is.False, "three taken: no more");
                barrelAtTwo |= ChestUpgrades.Offer(run, 2, 4, equipped, v, two, false).Any(u => u.Kind == UpgradeKind.LongerBarrel);
            }
            Assert.That(mendWhenHurt, Is.True, "hurt, Mend comes up");
            Assert.That(barrelAtTwo, Is.True, "with two taken it still comes up");
            Assert.That(ChestUpgrades.MayOffer(v.Upgrades.Single(r => r.Kind == UpgradeKind.LongerBarrel), two, true), Is.True);
            Assert.That(ChestUpgrades.MayOffer(v.Upgrades.Single(r => r.Kind == UpgradeKind.LongerBarrel), three, false), Is.False);
        }

        [Test]
        public void TheNewRows_HaveTheirWords()
        {
            Assert.That(Step(UpgradeKind.Mend, 0f, 30).Text, Is.EqualTo("Mend: heal 30 now (your maximum stays)"));
            Assert.That(Step(UpgradeKind.LongerBarrel, 1f).Short, Is.EqualTo("+1 m reach"));
            Assert.That(Step(UpgradeKind.LastingMarks, 2f).Short, Is.EqualTo("+2 s marks"));
            Assert.That(Step(UpgradeKind.KeenFocus, 0.1f).Short, Is.EqualTo("+10% focus"));
            Assert.That(Step(UpgradeKind.HeavyHits, 0.1f).Short, Is.EqualTo("+10% stagger"));
            foreach (UpgradeKind k in new[] { UpgradeKind.LongerBarrel, UpgradeKind.LastingMarks, UpgradeKind.KeenFocus, UpgradeKind.HeavyHits })
                StringAssert.Contains(":", Step(k, 1f).Text, k.ToString());
        }
    }
}
