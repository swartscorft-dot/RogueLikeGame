using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 109 (2026-11-24, Kristian's yes by name to the producer's three new skills): Ricochet, Smoke Round and Deadeye
    /// as rules. Each one's numbers and input type; Ricochet's targets (three hits at most, the nearest within its reach, never the
    /// same twice, not past cover), damage and marks; Smoke Round's cloud (inside, through, beside, up and gone) and the melee rule;
    /// Deadeye's targets (marked, in sight, in reach, nearest first, six at most), its spender's damage and its refusal; the six
    /// rows of the elite room's table; the pool of eleven and its offers with the switch on and off, and the odds of meeting each
    /// new skill in a run; her sheet's lines, the cards, the sounds and the session summary's block.
    /// </summary>
    public class NewSkillsTests
    {
        static readonly SkillDefinition[] s_Kit = SkillDefaults.Create();
        static SkillDefinition Skill(string name) => s_Kit.Single(s => s.Name == name);
        static int SlotOf(string name) => Array.FindIndex(s_Kit, s => s.Name == name);
        static ulong Seed(int i) => LevelRandom.Mix((ulong)i * 0x9E3779B97F4A7C15UL + 109UL) & LevelSeed.ShortMask;
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        /// <summary>The twelve of work order 015, in their order: the list with the switch off.</summary>
        static readonly string[] Twelve =
        {
            "Quick Shot", "Powder Keg", "Fan Fire", "Long Shot", "Scatter Blast", "Heavy Volley", "Backstep", "Triple Tap",
            "Tag Round", "Breaker Slug", "Slide", "Stopping Shot",
        };

        static TargetInfo T(float x, float z, bool alive = true, float radius = 0.5f) => new TargetInfo(new GroundPoint(x, z), radius, alive);

        // ---------------- The three ----------------

        [Test]
        public void TheKit_HasFifteen_TheThreeAtTheEnd_NoneToStart()
        {
            Assert.That(s_Kit.Length, Is.EqualTo(15));
            Assert.That(s_Kit.Take(12).Select(s => s.Name), Is.EqualTo(Twelve), "the twelve where they were");
            Assert.That(s_Kit.Skip(12).Select(s => s.Name), Is.EqualTo(NewSkillRules.Names));
            Assert.That(s_Kit.Skip(12).Any(s => s.StartEquipped), Is.False, "out of the pool, as the other eight");
            foreach (string n in NewSkillRules.Names) Assert.That(NewSkillRules.IsNew(n), Is.True, n);
            foreach (string n in Twelve) Assert.That(NewSkillRules.IsNew(n), Is.False, n);
        }

        [Test]
        public void Ricochet_IsALightPressOfAPistolLine_UnderQuickShotsDamage_ThatMarks_Every5s()
        {
            SkillDefinition r = Skill(NewSkillRules.Ricochet);
            Assert.That(r.Input, Is.EqualTo(SkillInput.Normal), "the skill model's Normal: a press");
            Assert.That(r.Shape, Is.EqualTo(SkillShape.Line));
            Assert.That(r.Lock, Is.EqualTo(SkillLock.Light));
            Assert.That(r.Damage, Is.LessThan(Skill("Quick Shot").Damage), "modest: under a Quick Shot's");
            Assert.That(r.MarksAdded, Is.EqualTo(1));
            Assert.That(r.RicochetJumps, Is.EqualTo(2), "two jumps: three hits at most");
            Assert.That(r.RicochetReach, Is.EqualTo(5f));
            Assert.That(r.Cooldown, Is.EqualTo(5f));
            Assert.That(SkillDefaults.MarkRole(r), Is.EqualTo("builds marks"));
            Assert.That(FlatDamage.WeightOf(r), Is.EqualTo(1f), "a quick single shot: her flat damage once a hit");
        }

        [Test]
        public void SmokeRound_IsALightPointPress_ACloud35mRound_For3s_10mAway_NoDamageNoMarks_Every14s()
        {
            SkillDefinition s = Skill(NewSkillRules.SmokeRound);
            Assert.That(s.Input, Is.EqualTo(SkillInput.Point), "the skill model's Point: it lands on the ground point under the cursor");
            Assert.That(s.Shape, Is.EqualTo(SkillShape.Circle));
            Assert.That(s.Lock, Is.EqualTo(SkillLock.Light));
            Assert.That(s.Range, Is.EqualTo(10f));
            Assert.That(s.Radius, Is.EqualTo(3.5f));
            Assert.That(s.SmokeSeconds, Is.EqualTo(3f));
            Assert.That(s.Damage, Is.Zero);
            Assert.That(s.MarksAdded + s.CleanTapMarks, Is.Zero);
            Assert.That(s.Cooldown, Is.EqualTo(14f));
            Assert.That(SkillDefaults.MarkRole(s), Is.EqualTo("no marks"));
            Assert.That(FlatDamage.WeightOf(s), Is.Zero);
        }

        [Test]
        public void Deadeye_IsAHalfSecondCast_ThatSpendsMarks_SixAtMost_Every12s()
        {
            SkillDefinition d = Skill(NewSkillRules.Deadeye);
            Assert.That(d.Input, Is.EqualTo(SkillInput.Casting), "the skill model's cast: a bar, then the shots");
            Assert.That(d.Startup, Is.EqualTo(0.5f));
            Assert.That(d.DeadeyeMost, Is.EqualTo(6));
            Assert.That(d.MarkDamageBonus, Is.GreaterThan(0f), "a spender");
            Assert.That(d.Cooldown, Is.EqualTo(12f));
            Assert.That(SkillDefaults.MarkRole(d), Is.EqualTo("spends marks"));
            Assert.That(MarkRules.IsSpender(d), Is.True);
            Assert.That(FlatDamage.WeightOf(d), Is.EqualTo(1f), "each shot a single pistol shot");
        }

        [Test]
        public void TheTiers_ByTheStandingRule_RareIsDamageAndStagger_SmokeRoundsIsItsCooldown()
        {
            var v = new SkillTierValues();
            foreach (string n in new[] { NewSkillRules.Ricochet, NewSkillRules.Deadeye })
                Assert.That(SkillTiers.Step(Skill(n), v).Patches.Select(p => p.ToString()), Is.EqualTo(new[] { "Damage x 1.3", "Stagger x 1.3" }), n);
            Assert.That(SkillTiers.Step(Skill(NewSkillRules.SmokeRound), v).Patches.Select(p => p.ToString()), Is.EqualTo(new[] { "Cooldown x 0.8" }),
                        "no damage: as Slide, 20 % shorter");
        }

        // ---------------- Ricochet's targets ----------------

        static List<int> Chain(IReadOnlyList<TargetInfo> t, int first, int jumps = 2, float reach = 5f, CoverLine cover = null)
        {
            var into = new List<int>();
            NewSkillRules.Chain(t, first, jumps, reach, cover, into);
            return into;
        }

        [Test]
        public void Ricochet_ThreeInARow_HitsAllThree_InOrder()
        {
            var t = new[] { T(0f, 4f), T(0f, 8f), T(0f, 12f) };
            Assert.That(Chain(t, 0), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void Ricochet_OneAlone_IsOneHit_AndAMiss_IsNone()
        {
            Assert.That(Chain(new[] { T(0f, 4f) }, 0), Is.EqualTo(new[] { 0 }));
            Assert.That(Chain(new[] { T(0f, 4f) }, -1), Is.Empty);
        }

        [Test]
        public void Ricochet_JumpsToTheNearestOther_NeverTheSameTwice_AtMostThreeHits()
        {
            // Two near each other: a, b; no ping-pong back to a.
            Assert.That(Chain(new[] { T(0f, 4f), T(2f, 4f) }, 0), Is.EqualTo(new[] { 0, 1 }), "two, never the first again");
            // From a, b is nearer than c; then c from b; d is one more than it may take.
            var t = new[] { T(0f, 0f), T(4f, 0f), T(1.5f, 0f), T(3f, 2f), T(5f, 2f) };
            List<int> hit = Chain(t, 0);
            Assert.That(hit.Count, Is.EqualTo(3), "three hits at most");
            Assert.That(hit[1], Is.EqualTo(2), "the nearest to the first");
            Assert.That(hit.Distinct().Count(), Is.EqualTo(3));
            Assert.That(Chain(t, 0, jumps: 3).Count, Is.EqualTo(4), "Fourth Bounce's one more");
        }

        [Test]
        public void Ricochet_NoJump_PastItsReach_ToTheDead_OrThroughCover()
        {
            Assert.That(Chain(new[] { T(0f, 0f), T(5.5f, 0f) }, 0), Is.EqualTo(new[] { 0 }), "5.5 m: out of its 5 m");
            Assert.That(Chain(new[] { T(0f, 0f), T(4.9f, 0f) }, 0), Is.EqualTo(new[] { 0, 1 }), "4.9 m: in");
            Assert.That(Chain(new[] { T(0f, 0f), T(2f, 0f, alive: false), T(4f, 0f) }, 0), Is.EqualTo(new[] { 0, 2 }), "over the dead one");
            // A wall at x = 1 stops every line that crosses it.
            CoverLine wall = (a, b) => (a.X < 1f) == (b.X < 1f) ? a.DistanceTo(b) : Math.Abs(1f - a.X) / Math.Max(1e-4f, Math.Abs(b.X - a.X)) * a.DistanceTo(b);
            Assert.That(Chain(new[] { T(0f, 0f), T(3f, 0f), T(0f, 4f) }, 0, cover: wall), Is.EqualTo(new[] { 0, 2 }), "the near one behind the wall is left out");
            Assert.That(Chain(new[] { T(0f, 0f, alive: false), T(1f, 0f) }, 0), Is.Empty, "a dead first: nothing");
        }

        [Test]
        public void Ricochet_EachHitDealsItsDamage_AndItsMark()
        {
            SkillDefinition r = Skill(NewSkillRules.Ricochet);
            Assert.That(SkillRules.Damage(r, 1f), Is.EqualTo(70), "every hit the same, the first and the jumps");
            Assert.That(r.MarksAdded, Is.EqualTo(1), "a mark each");
            Assert.That(r.MarkDamageBonus + r.MarkStaggerBonus, Is.Zero, "it spends none");
        }

        // ---------------- Deadeye's targets ----------------

        static List<int> Targets(IReadOnlyList<TargetInfo> t, IReadOnlyList<int> marks, float range = 12f, int most = 6, CoverLine cover = null)
        {
            var into = new List<int>();
            NewSkillRules.DeadeyeTargets(t, marks, new GroundPoint(0f, 0f), range, most, cover, into);
            return into;
        }

        [Test]
        public void Deadeye_ShootsEveryMarkedOne_NearestFirst_LeavesTheUnmarked()
        {
            var t = new[] { T(0f, 9f), T(0f, 3f), T(4f, 0f), T(0f, -6f) };
            var marks = new[] { 2, 1, 0, 5 };
            Assert.That(Targets(t, marks), Is.EqualTo(new[] { 1, 3, 0 }), "3 m, 6 m, 9 m; the one with no mark left out");
        }

        [Test]
        public void Deadeye_SixAtMost_TheNearestSix()
        {
            var t = Enumerable.Range(1, 9).Select(i => T(i, 0f)).ToArray();
            var marks = Enumerable.Repeat(1, 9).ToArray();
            Assert.That(Targets(t, marks), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }));
            Assert.That(Targets(t, marks, most: 8).Count, Is.EqualTo(8));
        }

        [Test]
        public void Deadeye_OnlyInHerReach_InSightPastCover_AndAlive()
        {
            var t = new[] { T(12.4f, 0f, radius: 0.5f), T(12.6f, 0f, radius: 0.5f), T(0f, 4f, alive: false), T(-3f, 0f) };
            var marks = new[] { 1, 1, 3, 1 };
            CoverLine wall = (a, b) => (a.X > -1f) == (b.X > -1f) ? a.DistanceTo(b) : Math.Abs(-1f - a.X) / Math.Max(1e-4f, Math.Abs(b.X - a.X)) * a.DistanceTo(b);
            Assert.That(Targets(t, marks), Is.EqualTo(new[] { 3, 0 }), "its near edge within 12 m; the dead left out");
            Assert.That(Targets(t, marks, cover: wall), Is.EqualTo(new[] { 0 }), "the one behind the wall left out");
        }

        [Test]
        public void Deadeye_WithNoMarkedOneInSight_HasNoTarget_SoItsPressIsRefused()
        {
            Assert.That(Targets(new[] { T(0f, 3f), T(2f, 2f) }, new[] { 0, 0 }), Is.Empty, "none marked");
            Assert.That(Targets(Array.Empty<TargetInfo>(), Array.Empty<int>()), Is.Empty, "none at all");
        }

        [Test]
        public void Deadeye_EachShot_DealsItsDamage_And40PercentMore_AMarkItSpends()
        {
            SkillDefinition d = Skill(NewSkillRules.Deadeye);
            Assert.That(NewSkillRules.DeadeyeDamage(d.Damage, 0, d.MarkDamageBonus), Is.EqualTo(80));
            Assert.That(NewSkillRules.DeadeyeDamage(d.Damage, 1, d.MarkDamageBonus), Is.EqualTo(112));
            Assert.That(NewSkillRules.DeadeyeDamage(d.Damage, 5, d.MarkDamageBonus), Is.EqualTo(240));
            Assert.That(NewSkillRules.DeadeyeDamage(d.Damage, 3, d.MarkDamageBonus), Is.EqualTo(MarkRules.SpenderDamage(d.Damage, 3, d.MarkDamageBonus)), "as a spender's");
        }

        // ---------------- Smoke Round's cloud ----------------

        static SmokeCloud Cloud(float x = 0f, float z = 0f, float r = 3.5f, float from = 0f, float until = 3f) => new SmokeCloud(new GroundPoint(x, z), r, from, until);

        [Test]
        public void TheCloud_HidesHer_FromOneInIt_AndFromOneWhoseLineCrossesIt_NotFromOneBeside()
        {
            var c = new[] { Cloud() };
            var her = new GroundPoint(10f, 0f);
            Assert.That(NewSkillRules.Hides(c, 1, new GroundPoint(1f, 1f), her, 1f), Is.True, "it stands in it");
            Assert.That(NewSkillRules.Hides(c, 1, new GroundPoint(-8f, 0f), her, 1f), Is.True, "its line to her crosses it");
            Assert.That(NewSkillRules.Hides(c, 1, new GroundPoint(-8f, 6f), new GroundPoint(10f, 6f), 1f), Is.False, "a line 6 m beside it");
            Assert.That(NewSkillRules.Hides(c, 1, new GroundPoint(8f, 5f), her, 1f), Is.False, "the cloud behind it, not between");
            Assert.That(NewSkillRules.Hides(c, 1, new GroundPoint(-8f, 0f), new GroundPoint(0.5f, 0f), 1f), Is.True, "she stands in it");
        }

        [Test]
        public void TheCloud_StandsFromItsLanding_ForItsSeconds_ThenHidesNothing()
        {
            var c = new[] { Cloud(from: 2f, until: 5f) };
            var e = new GroundPoint(1f, 0f);
            var her = new GroundPoint(10f, 0f);
            Assert.That(NewSkillRules.Hides(c, 1, e, her, 1.9f), Is.False, "before it landed");
            Assert.That(NewSkillRules.Hides(c, 1, e, her, 2f), Is.True);
            Assert.That(NewSkillRules.Hides(c, 1, e, her, 4.99f), Is.True);
            Assert.That(NewSkillRules.Hides(c, 1, e, her, 5f), Is.False, "cleared");
            Assert.That(NewSkillRules.Hides(c, 0, e, her, 3f), Is.False, "no cloud counted");
            Assert.That(NewSkillRules.Hides(null, 1, e, her, 3f), Is.False);
            Assert.That(new SmokeCloud(new GroundPoint(0f, 0f), 0f, 0f, 9f).IsUp(1f), Is.False, "no size: no cloud");
        }

        [Test]
        public void InTheSmoke_AMeleeEnemy_BeginsAnAttackOnlyWithHerInItsReach()
        {
            Assert.That(NewSkillRules.MeleeMayAttack(hidden: false, distance: 8f, SmokeValues.MeleeReach), Is.True, "no smoke: as before");
            Assert.That(NewSkillRules.MeleeMayAttack(hidden: true, distance: 8f, SmokeValues.MeleeReach), Is.False);
            Assert.That(NewSkillRules.MeleeMayAttack(hidden: true, distance: SmokeValues.MeleeReach, SmokeValues.MeleeReach), Is.True, "within its reach");
            Assert.That(SmokeValues.MeleeReach, Is.EqualTo(3f));
        }

        // ---------------- Their upgrade rows ----------------

        [Test]
        public void TheTable_EndsWithSixRows_TwoOfEachNewSkill_AChangeAndANumbersEach_AfterTheTwentySeven()
        {
            SkillUpgradeDefinition[] table = SkillUpgradeTable.Create();
            SkillUpgradeDefinition[] six = table.Skip(27).Take(6).ToArray();
            Assert.That(six.Select(u => u.Skill), Is.EqualTo(new[] { "Ricochet", "Ricochet", "Smoke Round", "Smoke Round", "Deadeye", "Deadeye" }));
            Assert.That(table.Take(27).Any(u => NewSkillRules.IsNew(u.Skill)), Is.False, "the 27 as they were");
            foreach (string n in NewSkillRules.Names)
            {
                SkillUpgradeDefinition[] mine = six.Where(u => u.Skill == n).ToArray();
                Assert.That(mine.Select(u => u.Kind), Is.EquivalentTo(new[] { SkillUpgradeKind.Change, SkillUpgradeKind.Numbers }), n);
                foreach (SkillUpgradeDefinition u in mine)
                    foreach (SkillPatch p in u.Patches) Assert.That(p.IsValid, Is.True, u.FullName + " " + p);
            }
        }

        static SkillDefinition With(string skill, string row) =>
            RunSkillUpgrades.Patched(Skill(skill), new[] { SkillUpgradeTable.Find(SkillUpgradeTable.Create(), skill, row) });

        [Test]
        public void EachOfTheSixRows_DoesWhatItsLineSays()
        {
            Assert.That(With("Ricochet", "Fourth Bounce").RicochetJumps, Is.EqualTo(3));
            Assert.That(With("Ricochet", "Long Bounce").RicochetReach, Is.EqualTo(8f));
            Assert.That(With("Smoke Round", "Twin Smoke").Charges, Is.EqualTo(2));
            Assert.That(With("Smoke Round", "Thick Smoke").SmokeSeconds, Is.EqualTo(5f));
            Assert.That(With("Deadeye", "Fast Hands").Startup, Is.EqualTo(0.2f));
            Assert.That(With("Deadeye", "Hollow Points").MarkDamageBonus, Is.EqualTo(0.6f));
            Assert.That(Skill("Ricochet").RicochetJumps, Is.EqualTo(2), "the settings' own untouched");
        }

        [Test]
        public void TheirCards_SayHowTheyArePressed_AndTheirRows_ShowTheirNumbers()
        {
            var c = new OfferContext();
            foreach (string n in NewSkillRules.Names)
            {
                OfferCard card = OfferTexts.ForSkill(Skill(n), null, "W", c);
                Assert.That(card.Sentence, Does.StartWith("Tap"), n);
                Assert.That(card.Sentence.Length, Is.InRange(30, 80), n);
                Assert.That(SkillDefaults.Line(n), Is.Not.Empty, n);
            }
            foreach (SkillUpgradeDefinition u in SkillUpgradeTable.Create().Where(u => NewSkillRules.IsNew(u.Skill)))
            {
                OfferCard card = OfferTexts.ForSkillUpgrade(u, Skill(u.Skill), "W", c);
                Assert.That(card.Numbers, Is.Not.Empty, u.FullName);
            }
            Assert.That(OfferTexts.ForSkillUpgrade(SkillUpgradeTable.Find(SkillUpgradeTable.Create(), "Ricochet", "Fourth Bounce"), Skill("Ricochet"), "W", c).Numbers,
                        Has.Some.Contains("jumps"));
            Assert.That(OfferTexts.ForSkillUpgrade(SkillUpgradeTable.Find(SkillUpgradeTable.Create(), "Deadeye", "Fast Hands"), Skill("Deadeye"), "W", c).Numbers,
                        Has.Some.StartWith("cast"));
        }

        [Test]
        public void HerSheet_SaysEachOnesOwn()
        {
            var c = new OfferContext();
            Assert.That(HerSheet.Skill(Skill("Ricochet"), "W", c).Lines.Select(l => l.Label), Has.Member("jumps"));
            Assert.That(HerSheet.Skill(Skill("Smoke Round"), "W", c).Lines.Select(l => l.Label), Has.Member("smoke"));
            Assert.That(HerSheet.Skill(Skill("Deadeye"), "W", c).Lines.Select(l => l.Label), Has.Member("targets").And.Member("cast"));
        }

        // ---------------- The pool, the offers and the switch ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_AndOff_TheListIsTheTwelve_AndTheTableTheTwentySeven()
        {
            Assert.That(new RunValues().NewSkills, Is.True);
            Assert.That(NewSkillRules.Switched(s_Kit, true), Is.SameAs(s_Kit), "on: the list itself");
            Assert.That(NewSkillRules.Switched(s_Kit, false).Select(s => s.Name), Is.EqualTo(Twelve), "off: the twelve, in their order");
            SkillUpgradeDefinition[] table = SkillUpgradeTable.Create();
            Assert.That(SkillUpgradeTable.Switched(table, new RunValues()), Is.SameAs(table), "on: the table itself");
            SkillUpgradeDefinition[] off = SkillUpgradeTable.Switched(table, new RunValues { NewSkills = false });
            Assert.That(off.Any(u => NewSkillRules.IsNew(u.Skill)), Is.False);
            Assert.That(off.Take(27), Is.EqualTo(table.Take(27)), "the 27 as they were, in their order");
        }

        static List<bool> StartHas(IReadOnlyList<SkillDefinition> skills) => skills.Select(s => s.StartEquipped).ToList();

        [Test]
        public void On_TheFloorReward_OffersThreeOfEleven_TheNewOnesAmongThem()
        {
            List<bool> has = StartHas(s_Kit);
            Assert.That(has.Count(h => !h), Is.EqualTo(11), "eleven in the pool");
            var seen = new HashSet<int>();
            for (int i = 0; i < 300; i++)
            {
                List<int> offer = FloorRewards.Offer(Seed(i), 1, has, 3);
                Assert.That(offer.Count, Is.EqualTo(3));
                Assert.That(offer.All(s => !s_Kit[s].StartEquipped), Is.True);
                seen.UnionWith(offer);
            }
            foreach (string n in NewSkillRules.Names) Assert.That(seen, Has.Member(SlotOf(n)), n + " is offered");
        }

        [Test]
        public void Off_TheOffersAreAsBefore_FieldForField()
        {
            // Before work order 109 her list was the twelve: the same twelve with the switch off draw the same slots, which
            // name the same skills, on every floor.
            SkillDefinition[] off = NewSkillRules.Switched(s_Kit, false);
            SkillDefinition[] before = s_Kit.Take(12).Select(s => s.Copy()).ToArray();
            for (int i = 0; i < 300; i++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    List<int> now = FloorRewards.Offer(Seed(i), floor, StartHas(off), 3);
                    List<int> then = FloorRewards.Offer(Seed(i), floor, StartHas(before), 3);
                    Assert.That(now.Select(s => off[s].Name), Is.EqualTo(then.Select(s => before[s].Name)));
                }
        }

        /// <summary>
        /// The odds of meeting each new skill in a run (work order 109: "Say what the odds of seeing each new skill in a run are"):
        /// 20 000 seeded runs through the rules, four floor rewards each, three cards a reward, the first card taken (as the
        /// test pilot takes it). Printed, and pinned as ranges round what the arithmetic gives: on floor 1 a new skill is on one of
        /// the three cards 3 times in 11 (27 %); offered at least once in a run about 3 runs in 4; with none of it ever taken, 1 -
        /// 8/11 x 7/10 x 6/9 x 5/8 = 79 %. At least one of the three is offered in nearly every run.
        /// </summary>
        [Test]
        public void TheOdds_OfMeetingEachNewSkill_InARun()
        {
            const int Runs = 20000;
            var offered = new Dictionary<string, int>();
            var onFloor1 = new Dictionary<string, int>();
            var taken = new Dictionary<string, int>();
            foreach (string n in NewSkillRules.Names) offered[n] = onFloor1[n] = taken[n] = 0;
            int anyNew = 0;
            for (int i = 0; i < Runs; i++)
            {
                List<bool> has = StartHas(s_Kit);
                var met = new HashSet<string>();
                for (int floor = 1; floor <= 4; floor++)
                {
                    List<int> offer = FloorRewards.Offer(Seed(i), floor, has, 3);
                    foreach (int s in offer)
                        if (NewSkillRules.IsNew(s_Kit[s].Name))
                        {
                            met.Add(s_Kit[s].Name);
                            if (floor == 1) onFloor1[s_Kit[s].Name]++;
                        }
                    has[offer[0]] = true;
                    if (NewSkillRules.IsNew(s_Kit[offer[0]].Name)) taken[s_Kit[offer[0]].Name]++;
                }
                foreach (string n in met) offered[n]++;
                if (met.Count > 0) anyNew++;
            }
            foreach (string n in NewSkillRules.Names)
            {
                float o = offered[n] / (float)Runs, f = onFloor1[n] / (float)Runs, t = taken[n] / (float)Runs;
                TestContext.Out.WriteLine(string.Format(s_Inv, "{0}: offered in {1:0.0}% of runs, on floor 1 in {2:0.0}%, taken (the first card) in {3:0.0}%",
                                                        n, o * 100f, f * 100f, t * 100f));
                Assert.That(f, Is.EqualTo(3f / 11f).Within(0.015f), n + " on floor 1: 3 in 11");
                Assert.That(o, Is.InRange(0.68f, 0.82f), n + " offered in a run");
                Assert.That(t, Is.InRange(0.25f, 0.45f), n + " taken");
            }
            TestContext.Out.WriteLine(string.Format(s_Inv, "One of the three at least: {0:0.0}% of runs", anyNew * 100f / Runs));
            Assert.That(anyNew / (float)Runs, Is.GreaterThan(0.97f));
        }

        // ---------------- The sounds ----------------

        [Test]
        public void TheirThreeSounds_AreAtTheEnd_EachWithItsLine_QuietBesideTheGuns()
        {
            var v = new PlaceholderSoundValues();
            PlaceholderSound[] three = { PlaceholderSound.RicochetJump, PlaceholderSound.SmokePuff, PlaceholderSound.DeadeyeAim };
            Assert.That(PlaceholderSoundRules.All.Skip(46).Take(3), Is.EqualTo(three), "after the forty-six before them in the merged list: other orders of the day sprint may follow (changed at the merge, 2026-11-24)");
            Assert.That((int)PlaceholderSound.OffTick, Is.EqualTo(37), "the 38 where they were");
            foreach (PlaceholderSound s in three)
            {
                Assert.That(v.Sounds.Count(x => x.Sound == s), Is.EqualTo(1), s.ToString());
                Assert.That(v.Volume(s), Is.InRange(0.2f, 0.5f), s.ToString());
                float[] samples = Synth.Render(PlaceholderRecipes.For(s), Synth.SampleRate);
                Assert.That(samples.Max(x => Math.Abs(x)), Is.InRange(0.3f, 0.9001f), s.ToString());
                Assert.That(samples[0], Is.EqualTo(0f));
                Assert.That(samples[samples.Length - 1], Is.EqualTo(0f));
            }
        }

        // ---------------- The session summary ----------------

        [Test]
        public void TheSummary_HasTheirBlock_OnlyWhenOneWasUsed()
        {
            var events = new List<SessionEvent>
            {
                new SessionEvent(0d, SessionEventTypes.SessionStart),
                new SessionEvent(1d, SessionEventTypes.SkillFired, "Ricochet") { Hit = true },
                new SessionEvent(1d, SessionEventTypes.Hit, "Ricochet") { Damage = 70 },
                new SessionEvent(1d, SessionEventTypes.Hit, "Ricochet") { Damage = 70 },
                new SessionEvent(1d, SessionEventTypes.RicochetJump, "Ricochet") { Shot = 2 },
                new SessionEvent(2d, SessionEventTypes.SmokeCloud, "Smoke Round"),
                new SessionEvent(3d, SessionEventTypes.DeadeyeVolley, "Deadeye") { Shot = 3 },
                new SessionEvent(4d, SessionEventTypes.SkillPress, "Deadeye") { Result = SessionWords.Rejected, Reason = SessionWords.NoMarkedTarget },
                new SessionEvent(5d, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.NewSkills.RicochetUses, Is.EqualTo(1));
            Assert.That(stats.NewSkills.RicochetHits, Is.EqualTo(2));
            Assert.That(stats.NewSkills.RicochetJumps, Is.EqualTo(1));
            Assert.That(stats.NewSkills.SmokeClouds, Is.EqualTo(1));
            Assert.That(stats.NewSkills.DeadeyeVolleys, Is.EqualTo(1));
            Assert.That(stats.NewSkills.DeadeyeShots, Is.EqualTo(3));
            Assert.That(stats.NewSkills.DeadeyeRefused, Is.EqualTo(1));
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("The new skills (work order 109)", text);
            StringAssert.Contains("1 / 2 / 1", text);
            string none = SessionSummary.Format(SessionSummary.Compute(new List<SessionEvent> { events[0], events[events.Count - 1] }));
            StringAssert.DoesNotContain("work order 109", none);
        }
    }
}
