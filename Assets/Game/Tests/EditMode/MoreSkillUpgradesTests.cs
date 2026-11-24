using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 111 (2026-11-24, Kristian said yes to "De åtte skillsene som har ett eller to valg, får opptil tre (27 → 36)"):
    /// the nine rows added at the end of the elite room's table, as rules. Each row's effect on the skill; the table's count with
    /// the switches on and off (42; 33 with this one off; 27 with both off); every one of the eleven old and new skills with three
    /// rows or two, as the order has them; each of the nine a Change, so an epic of each of the eight skills can draw it; and the
    /// cards and her sheet showing them as they show the others.
    /// </summary>
    public class MoreSkillUpgradesTests
    {
        static readonly SkillDefinition[] s_Kit = SkillDefaults.Create();
        static readonly SkillUpgradeDefinition[] s_Table = SkillUpgradeTable.Create();
        static SkillDefinition Skill(string name) => s_Kit.Single(s => s.Name == name);
        static SkillUpgradeDefinition Row(string skill, string name) => SkillUpgradeTable.Find(s_Table, skill, name);
        static SkillDefinition With(string skill, string row) => RunSkillUpgrades.Patched(Skill(skill), new[] { Row(skill, row) });

        /// <summary>The nine, in the order they stand at the table's end.</summary>
        static readonly string[][] Nine =
        {
            new[] { "Long Shot", "Tracer Round" }, new[] { "Powder Keg", "Concussion Keg" }, new[] { "Fan Fire", "Focused Fire" },
            new[] { "Heavy Volley", "Breaching Volley" }, new[] { "Tag Round", "Painted Target" }, new[] { "Slide", "Gun Slide" },
            new[] { "Stopping Shot", "Marked Opening" }, new[] { "Breaker Slug", "Recoil Slug" }, new[] { "Breaker Slug", "Heavy Slug" },
        };

        [Test]
        public void TheTable_Is42_TheNineAtTheEnd_AfterThe27AndWorkOrder109sSix()
        {
            Assert.That(s_Table.Length, Is.EqualTo(42));
            Assert.That(s_Table.Skip(33).Select(u => new[] { u.Skill, u.Name }), Is.EqualTo(Nine));
            Assert.That(s_Table.Skip(27).Take(6).All(u => NewSkillRules.IsNew(u.Skill)), Is.True, "109's six before them");
            Assert.That(s_Table.Select(u => u.FullName).Distinct().Count(), Is.EqualTo(42), "one name each");
            foreach (SkillUpgradeDefinition u in s_Table.Skip(33))
            {
                Assert.That(u.Kind, Is.EqualTo(SkillUpgradeKind.Change), u.FullName + ": a change she feels in how it is used");
                Assert.That(u.Patches, Is.Not.Empty, u.FullName);
                foreach (SkillPatch p in u.Patches) Assert.That(p.IsValid, Is.True, u.FullName + " " + p);
                Assert.That(u.Line.Length, Is.InRange(15, 90), u.FullName);
            }
        }

        [Test]
        public void TheSwitch_IsOnByDefault_AndOff_TheTableIsThe27AndWorkOrder109sSix()
        {
            Assert.That(new RunValues().MoreSkillUpgrades, Is.True);
            Assert.That(SkillUpgradeTable.Switched(s_Table, new RunValues()), Is.SameAs(s_Table), "on: the table itself");
            SkillUpgradeDefinition[] off = SkillUpgradeTable.Switched(s_Table, new RunValues { MoreSkillUpgrades = false });
            Assert.That(off.Length, Is.EqualTo(33));
            Assert.That(off, Is.EqualTo(s_Table.Take(33)), "the 27 and the six, in their order");
            Assert.That(SkillUpgradeTable.Switched(s_Table, new RunValues { MoreSkillUpgrades = false, NewSkills = false }), Is.EqualTo(s_Table.Take(27)),
                        "both off: the table of work order 017");
            Assert.That(SkillUpgradeTable.Switched(s_Table, new RunValues { NewSkills = false }).Length, Is.EqualTo(36), "the 27 and the nine");
        }

        [Test]
        public void EverySkill_HasItsRows_ThreeForEachOfTheTwelve_AndTwoForEachNewOne()
        {
            foreach (SkillDefinition s in s_Kit)
            {
                int rows = s_Table.Count(u => u.Skill == s.Name);
                Assert.That(rows, Is.EqualTo(NewSkillRules.IsNew(s.Name) ? 2 : 3), s.Name);
            }
        }

        [Test]
        public void OffWithTheSwitch_TheEightHaveTheirOneOrTwoRows_AsBefore()
        {
            SkillUpgradeDefinition[] off = SkillUpgradeTable.Switched(s_Table, new RunValues { MoreSkillUpgrades = false });
            var before = new Dictionary<string, int>
            {
                { "Long Shot", 2 }, { "Powder Keg", 2 }, { "Fan Fire", 2 }, { "Heavy Volley", 2 }, { "Tag Round", 2 }, { "Slide", 2 },
                { "Stopping Shot", 2 }, { "Breaker Slug", 1 },
            };
            foreach (KeyValuePair<string, int> b in before) Assert.That(off.Count(u => u.Skill == b.Key), Is.EqualTo(b.Value), b.Key);
        }

        // ---------------- Each row's effect ----------------

        [Test]
        public void TracerRound_LongShotMarks_TwoOnEachEnemyItHits()
        {
            Assert.That(Skill("Long Shot").MarksAdded, Is.Zero, "today it marks nothing");
            SkillDefinition s = With("Long Shot", "Tracer Round");
            Assert.That(s.MarksAdded, Is.EqualTo(2));
            Assert.That(SkillDefaults.MarkRole(s), Is.EqualTo("builds marks"));
            Assert.That(Row("Long Shot", "Tracer Round").CannotGoWith(Row("Long Shot", "Snap Shot")), Is.False, "it goes with either of the others");
        }

        [Test]
        public void ConcussionKeg_TheBurstStaggers50_InsteadOf20()
        {
            Assert.That(Skill("Powder Keg").Stagger, Is.EqualTo(20f));
            Assert.That(With("Powder Keg", "Concussion Keg").Stagger, Is.EqualTo(50f).Within(1e-4f));
            Assert.That(With("Powder Keg", "Concussion Keg").Damage, Is.EqualTo(Skill("Powder Keg").Damage), "no more damage");
        }

        [Test]
        public void FocusedFire_ANarrowCone_ThatReachesFarther_AndHitsHarder()
        {
            SkillDefinition s = With("Fan Fire", "Focused Fire");
            Assert.That(s.Angle, Is.EqualTo(25f));
            Assert.That(s.Range, Is.EqualTo(10f));
            Assert.That(s.Damage, Is.EqualTo(45), "30 a tick, half as much again");
            Assert.That(SkillRules.Hits(s, new GroundPoint(0f, 0f), new GroundPoint(0f, 1f), new GroundPoint(0f, 9f), 0.1f), Is.True, "9 m ahead: in");
            Assert.That(SkillRules.Hits(s, new GroundPoint(0f, 0f), new GroundPoint(0f, 1f), new GroundPoint(1.8f, 4f), 0.1f), Is.False, "24° off: out");
            Assert.That(SkillRules.Hits(Skill("Fan Fire"), new GroundPoint(0f, 0f), new GroundPoint(0f, 1f), new GroundPoint(1.8f, 4f), 0.1f), Is.True,
                        "as before, the wide cone takes it");
        }

        [Test]
        public void BreachingVolley_EachMarkItSpends_Adds30PercentStagger()
        {
            SkillDefinition s = With("Heavy Volley", "Breaching Volley");
            Assert.That(s.MarkStaggerBonus, Is.EqualTo(0.3f));
            Assert.That(PoolRules.SpenderStagger(s.Stagger, 5, s.MarkStaggerBonus), Is.EqualTo(100f).Within(1e-3f), "40 at five marks: 100");
            Assert.That(s.MarkDamageBonus, Is.EqualTo(Skill("Heavy Volley").MarkDamageBonus), "its damage bonus stays");
            Assert.That(MarkRules.Spends(s, false), Is.True);
        }

        [Test]
        public void PaintedTarget_TagRoundPutsThree()
        {
            Assert.That(With("Tag Round", "Painted Target").MarksAdded, Is.EqualTo(3));
            Assert.That(With("Tag Round", "Painted Target").TagSeconds, Is.EqualTo(Skill("Tag Round").TagSeconds), "its tag as before");
        }

        [Test]
        public void GunSlide_BasicShotsThatHitDuringTheSlide_Deal50PercentMore()
        {
            SkillDefinition s = With("Slide", "Gun Slide");
            Assert.That(s.SlideShotDamage, Is.EqualTo(0.5f));
            Assert.That(MoreUpgradeRules.SlideShot(40, s.SlideShotDamage), Is.EqualTo(60));
            Assert.That(MoreUpgradeRules.SlideShot(40, 0f), Is.EqualTo(40), "without it, as before");
            Assert.That(Skill("Slide").SlideShotDamage, Is.Zero);
        }

        [Test]
        public void MarkedOpening_ACounterPutsThreeMarks()
        {
            Assert.That(With("Stopping Shot", "Marked Opening").CounterMarks, Is.EqualTo(3));
            Assert.That(Skill("Stopping Shot").CounterMarks, Is.Zero);
        }

        [Test]
        public void RecoilSlug_CarriesHer3mBack_AsTheSlugGoes()
        {
            SkillDefinition s = With("Breaker Slug", "Recoil Slug");
            Assert.That(s.MoveDistance, Is.EqualTo(-3f), "backward, along the aim, over its startup");
            Assert.That(s.OpeningMove, Is.False, "one press: the hop and the slug together");
        }

        [Test]
        public void HeavySlug_SpendsMarksForDamageToo_25PercentAMark()
        {
            SkillDefinition s = With("Breaker Slug", "Heavy Slug");
            Assert.That(s.MarkDamageBonus, Is.EqualTo(0.25f));
            Assert.That(MarkRules.SpenderDamage(s.Damage, 4, s.MarkDamageBonus), Is.EqualTo(300), "150 at four marks: 300");
            Assert.That(s.MarkStaggerBonus, Is.EqualTo(Skill("Breaker Slug").MarkStaggerBonus), "and its stagger per mark stays");
        }

        // ---------------- The tiers' epic step ----------------

        [Test]
        public void AnEpic_OfEachOfTheEightSkills_CanDrawItsNewRow()
        {
            var v = new SkillTierValues { Odds = new[] { 0f, 0f, 100f }, FirstRewardAtLeastRare = false }; // every card an epic
            foreach (string[] row in Nine)
            {
                int slot = Array.FindIndex(s_Kit, s => s.Name == row[0]);
                Assert.That(SkillTiers.OpenChanges(s_Table, row[0], Array.Empty<SkillUpgradeDefinition>()).Select(u => u.Name), Has.Member(row[1]), row[0]);
                bool drawn = false;
                for (int i = 0; i < 400 && !drawn; i++)
                {
                    ulong seed = LevelRandom.Mix((ulong)i * 0x9E3779B97F4A7C15UL + 111UL) & LevelSeed.ShortMask;
                    SkillTierOffer offer = SkillTiers.Draw(seed, 2, 0, new[] { slot }, s_Kit, s_Table, Array.Empty<SkillUpgradeDefinition>(), 0f, v, new StatValues())[0];
                    Assert.That(offer.Tier, Is.EqualTo(UpgradeTier.Epic));
                    drawn = offer.Change != null && offer.Change.Name == row[1];
                }
                Assert.That(drawn, Is.True, row[0] + ": an epic drew " + row[1]);
            }
            SkillUpgradeDefinition[] off = SkillUpgradeTable.Switched(s_Table, new RunValues { MoreSkillUpgrades = false });
            foreach (string[] row in Nine)
                Assert.That(SkillTiers.OpenChanges(off, row[0], Array.Empty<SkillUpgradeDefinition>()).Select(u => u.Name), Has.No.Member(row[1]), "off: " + row[0]);
        }

        [Test]
        public void TheEliteRoom_OffersTheNewRows_ToASkillSheHas()
        {
            var had = new HashSet<string> { "Breaker Slug" };
            List<SkillUpgradeDefinition> open = SkillUpgradeOffer.Open(s_Table, had, Array.Empty<SkillUpgradeDefinition>());
            Assert.That(open.Select(u => u.Name), Is.EquivalentTo(new[] { "Follow Through", "Recoil Slug", "Heavy Slug" }));
        }

        // ---------------- The cards and her sheet ----------------

        [Test]
        public void EachCard_ShowsWhatItsRowChanges_InNumbers()
        {
            var c = new OfferContext();
            foreach (string[] row in Nine)
            {
                OfferCard card = OfferTexts.ForSkillUpgrade(Row(row[0], row[1]), Skill(row[0]), "W", c);
                Assert.That(card.Numbers, Is.Not.Empty, row[0] + ": " + row[1]);
                Assert.That(card.Sentence, Does.EndWith("."));
                Assert.That(card.Sentence.Length, Is.InRange(15, 80));
                StringAssert.Contains("changes how it plays", card.For);
            }
            Assert.That(OfferTexts.ForSkillUpgrade(Row("Breaker Slug", "Recoil Slug"), Skill("Breaker Slug"), "W", c).Numbers, Has.Some.Contains("3 m back"));
            Assert.That(OfferTexts.ForSkillUpgrade(Row("Slide", "Gun Slide"), Skill("Slide"), "W", c).Numbers, Has.Some.Contains("+50"));
            Assert.That(OfferTexts.ForSkillUpgrade(Row("Stopping Shot", "Marked Opening"), Skill("Stopping Shot"), "W", c).Numbers, Has.Some.Contains("a counter marks"));
        }

        [Test]
        public void HerSheet_SaysWhatTheRowsAdd()
        {
            var c = new OfferContext();
            Assert.That(HerSheet.Skill(With("Slide", "Gun Slide"), "W", c).Lines.Select(l => l.ToString()), Has.Some.Contains("while sliding"));
            Assert.That(HerSheet.Skill(With("Stopping Shot", "Marked Opening"), "W", c).Lines.Select(l => l.ToString()), Has.Some.Contains("3 marks"));
            Assert.That(HerSheet.Skill(With("Long Shot", "Tracer Round"), "W", c).Lines.Select(l => l.Label), Has.Member("marks"));
        }
    }
}
