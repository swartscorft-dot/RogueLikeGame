using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 017 (2026-11-20): the elite room's place in the chain and its elite, the skill upgrades as data (every
    /// patch, the skill before and after), exclusions, the offer, the run's own view of the skills, the dash's chest step,
    /// and what the record and the summary say.
    /// </summary>
    public class SkillUpgradeTests
    {
        // ---------------- The elite room ----------------

        [Test]
        public void EliteRoomNumber_IsTheMiddleCombatRoom_TheLaterOfTwo()
        {
            var plan = new LevelPlan();
            Assert.That(plan.EliteRoomNumber(0), Is.EqualTo(0));
            Assert.That(plan.EliteRoomNumber(1), Is.EqualTo(1));
            Assert.That(plan.EliteRoomNumber(2), Is.EqualTo(2));
            Assert.That(plan.EliteRoomNumber(3), Is.EqualTo(2));
            Assert.That(plan.EliteRoomNumber(4), Is.EqualTo(3), "with four combat rooms, the third");
            Assert.That(plan.EliteRoomNumber(5), Is.EqualTo(3));
            Assert.That(plan.EliteRoomNumber(6), Is.EqualTo(4));
            plan.EliteRoom = false;
            Assert.That(plan.EliteRoomNumber(4), Is.EqualTo(0));
        }

        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 2)]
        [TestCase(4, 2)]
        [TestCase(5, 2)]
        [TestCase(6, 2)]
        public void Generator_MarksTheEliteRoom_ForEveryCountOfCombatRooms(int combatRooms, int treasureRooms)
        {
            var v = new LevelValues();
            v.Plan.CombatRooms = combatRooms;
            v.Plan.MoreCombatRooms = 0; // work order 046: that many in the row too
            v.Plan.TreasureRooms = treasureRooms;
            int made = 0;
            for (ulong s = 0; s < 40; s++)
            {
                LevelLayout level;
                try { level = LevelGenerator.Generate(s, 1 + (int)(s % 5), v); }
                catch (InvalidOperationException) { continue; }
                made++;
                Assert.That(LevelValidator.Validate(level, v), Is.Empty, "seed " + s + "\n" + LevelMap.Draw(level));
                List<LevelRoom> combats = LevelGeometry.MainPath(level).Where(r => r.Kind == RoomKind.Combat).ToList();
                Assert.That(combats.Count, Is.EqualTo(combatRooms));
                for (int i = 0; i < combats.Count; i++)
                    Assert.That(combats[i].Elite, Is.EqualTo(i + 1 == v.Plan.EliteRoomNumber(combatRooms)), "seed " + s + " combat room " + (i + 1));
                Assert.That(level.Rooms.Count(r => r.Elite), Is.EqualTo(1));
                Assert.That(level.EliteRoom.EnemyGroups.Count(g => g.Elite), Is.EqualTo(1));
                // Its own letter on the map (an enemy point may be drawn over the number beside it; no other room, enemy or prop is L).
                Assert.That(LevelMap.Draw(level).Split('\n').Skip(1).Sum(line => line.Count(c => c == 'L')), Is.LessThanOrEqualTo(1), "its own letter on the map");
                Assert.That(level.EliteRoom.Letter, Is.EqualTo('L'));
            }
            TestContext.WriteLine("ELITECOUNT combat rooms " + combatRooms + ": " + made + " of 40 seeds made");
            Assert.That(made, Is.GreaterThan(30), "the generator makes floors of this plan");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        public void TheElite_IsOfAKindTheFloorCanHave_AndEveryKindComesUpFromFloor2(int floor)
        {
            var v = new LevelValues { FloorCharacter = false }; // work order 104 (changed deliberately): 017's elites; with the floors' character the guard's elite comes a floor after the guard (FloorCharacterTests)
            var kinds = new HashSet<EnemyKind>();
            for (ulong s = 0; s < 300; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, floor, v);
                LevelRoom room = level.EliteRoom;
                Assert.That(room, Is.Not.Null);
                Assert.That(room.Kind, Is.EqualTo(RoomKind.Combat));
                EnemyGroup elite = room.EnemyGroups.Single(g => g.Elite);
                Assert.That(elite.Count, Is.EqualTo(1));
                Assert.That(elite.Boss, Is.False);
                Assert.That(elite.ToString(), Does.StartWith("elite "));
                kinds.Add(elite.Kind);
                // What a combat room holds besides it.
                v.EnemyRange(RoomKind.Combat, floor, out int fewest, out int most);
                Assert.That(room.EnemyGroups.Where(g => !g.Elite).Sum(g => g.Count), Is.InRange(fewest, most));
                Assert.That(level.Rooms.Where(r => !r.Elite).SelectMany(r => r.EnemyGroups).Any(g => g.Elite), Is.False);
                if (floor == 5) Assert.That(level.ExitRoom.EnemyGroups.Count(g => g.Boss), Is.EqualTo(1), "the act boss as before");
            }
            if (floor == 1) Assert.That(kinds, Is.EquivalentTo(new[] { EnemyKind.Brute }));
            // Work order 027 changed this on purpose: from floor 3 the elite may be a caster too; work order 034: only with
            // the casters on, which they were not by default (so the kinds were main's again) until work order 054 set them on.
            else if (floor < v.EliteCasterFromFloor || !v.Casters) Assert.That(kinds, Is.EquivalentTo(new[] { EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard }));
            else Assert.That(kinds, Is.EquivalentTo(new[] { EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster }));
        }

        [Test]
        public void ARunSeed_Floor1AndFloor5_EachHaveTheirEliteRoom()
        {
            const ulong run = 0x0E117E17UL; // the PlayMode tests' run seed
            foreach (int floor in new[] { 1, 5 })
            {
                LevelLayout level = LevelGenerator.Generate(RunSeeds.FloorSeed(run, floor), floor);
                Assert.That(LevelValidator.Validate(level), Is.Empty);
                Assert.That(level.EliteRoom, Is.Not.Null);
                TestContext.WriteLine("RUNMAP run " + LevelSeed.ToText(run) + " floor " + floor + ", elite " + level.EliteRoom.EnemyGroups.Single(g => g.Elite)
                                      + " in room " + level.EliteRoom.Order + "\n" + LevelMap.Draw(level));
            }
        }

        [Test]
        public void TheElitesNumbers_ItsKindsHealthByKind_AQuarterMoreDamage_AThirdLarger_HalfTheGuardsBar()
        {
            var v = new LevelValues();
            // Work order 030, lever C: by its kind (four times each before).
            Assert.That(v.EliteHealthOf(EnemyKind.Brute), Is.EqualTo(3 * v.EnemyHealth));
            Assert.That(v.EliteHealthOf(EnemyKind.Shooter), Is.EqualTo(5 * v.ShooterHealth));
            Assert.That(v.EliteHealthOf(EnemyKind.Guard), Is.EqualTo(2 * v.GuardHealth));
            Assert.That(v.EliteDamageScale, Is.EqualTo(1.25f));
            Assert.That(v.EliteSizeScale, Is.EqualTo(4f / 3f).Within(1e-6f));
            Assert.That(v.EliteStaggerShare, Is.EqualTo(0.5f));
            Assert.That(v.EliteKinds(1), Is.EqualTo(new[] { EnemyKind.Brute }));
            TestContext.WriteLine("ELITE health brute " + v.EliteHealthOf(EnemyKind.Brute) + ", shooter " + v.EliteHealthOf(EnemyKind.Shooter)
                                  + ", guard " + v.EliteHealthOf(EnemyKind.Guard));
        }

        [Test]
        public void Validator_ReportsAnEliteRoomInTheWrongPlace_AndAMissingElite()
        {
            LevelLayout level = LevelGenerator.Generate(42UL, 2);
            Assume.That(LevelValidator.Validate(level), Is.Empty);
            LevelRoom elite = level.EliteRoom;
            LevelRoom other = level.Rooms.First(r => r.Kind == RoomKind.Combat && !r.Elite);
            other.Elite = true;
            Assert.That(LevelValidator.Validate(level).Any(f => f.Contains("is marked elite")), string.Join("\n", LevelValidator.Validate(level)));
            other.Elite = false;
            int at = elite.EnemyGroups.FindIndex(g => g.Elite);
            elite.EnemyGroups[at] = new EnemyGroup(elite.EnemyGroups[at].Kind);
            Assert.That(LevelValidator.Validate(level).Any(f => f.Contains("0 elites")), string.Join("\n", LevelValidator.Validate(level)));
        }

        // ---------------- The table and its patches ----------------

        static SkillDefinition Default(string name) => SkillDefaults.Create().Single(s => s.Name == name);
        static SkillUpgradeDefinition Row(string skill, string name) => SkillUpgradeTable.Find(SkillUpgradeTable.Create(), skill, name);

        [Test]
        public void TheTable_EveryRowIsOfASkillInTheGame_WithALine_AndPatchesThatChangeSomething()
        {
            SkillUpgradeDefinition[] table = SkillUpgradeTable.Create();
            var names = new HashSet<string>(SkillDefaults.Create().Select(s => s.Name));
            Assert.That(table.Length, Is.EqualTo(42)); // changed deliberately (work order 109): its six rows after the 27; and (work order 111) its nine after those
            Assert.That(table.Select(u => u.FullName).Distinct().Count(), Is.EqualTo(table.Length));
            foreach (SkillUpgradeDefinition u in table)
            {
                Assert.That(names, Does.Contain(u.Skill), u.FullName);
                Assert.That(u.Line, Is.Not.Empty, u.FullName);
                Assert.That(u.Patches, Is.Not.Empty, u.FullName);
                foreach (SkillPatch p in u.Patches) Assert.That(p.IsValid, u.FullName + " " + p);
                foreach (string x in u.Excludes) Assert.That(table.Any(o => o.Skill == u.Skill && o.Name == x), u.FullName + " excludes " + x);
                SkillDefinition before = Default(u.Skill);
                SkillDefinition after = RunSkillUpgrades.Patched(before, new[] { u });
                Assert.That(Fields(after), Is.Not.EqualTo(Fields(before)), u.FullName + " changes the skill");
                Assert.That(Fields(before), Is.EqualTo(Fields(Default(u.Skill))), u.FullName + " leaves the skill it was given alone");
            }
            // Every skill of the game has at least one; every start skill at least three.
            foreach (SkillDefinition s in SkillDefaults.Create())
                Assert.That(table.Count(u => u.Skill == s.Name), Is.GreaterThanOrEqualTo(s.StartEquipped ? 3 : 1), s.Name);
            Assert.That(table.Any(u => u.Kind == SkillUpgradeKind.Change) && table.Any(u => u.Kind == SkillUpgradeKind.Numbers));
        }

        static string Fields(SkillDefinition s) =>
            string.Join(";", typeof(SkillDefinition).GetFields().Select(f => f.Name + "=" + f.GetValue(s)));

        static SkillDefinition After(string skill, string name) => RunSkillUpgrades.Patched(Default(skill), new[] { Row(skill, name) });

        [Test]
        public void EveryUpgrade_PatchesItsSkill_AsItsLineSays()
        {
            // The start skills.
            Assert.That(After("Quick Shot", "Double Tap").Charges, Is.EqualTo(2));
            Assert.That(After("Quick Shot", "Quick Draw").BasicHitCooldownCut, Is.EqualTo(0.3f));
            SkillDefinition heavy = After("Quick Shot", "Heavy Round");
            Assert.That((heavy.Damage, heavy.Stagger), Is.EqualTo((160, 6f)), "60 % more damage, twice the stagger");
            Assert.That(After("Triple Tap", "Fourth Tap").RecastCount, Is.EqualTo(4));
            SkillDefinition tied = After("Triple Tap", "Tied Trigger");
            Assert.That((tied.HitCooldownCut, tied.HitCutsSkill), Is.EqualTo((0.5f, "Scatter Blast")));
            Assert.That(After("Triple Tap", "Sharp Taps").Damage, Is.EqualTo(98), "70 and 40 %");
            SkillDefinition slug = After("Scatter Blast", "Slug");
            Assert.That((slug.Shape, slug.Range, slug.HalfWidth), Is.EqualTo((SkillShape.Line, 10f, 0.4f)));
            Assert.That(slug.MarkDamageBonus, Is.EqualTo(Default("Scatter Blast").MarkDamageBonus), "still a spender");
            SkillDefinition blank = After("Scatter Blast", "Point Blank");
            Assert.That((blank.CloseRange, blank.CloseMarkScale), Is.EqualTo((2.5f, 2f)));
            Assert.That(After("Scatter Blast", "Fresh Shells").KillCooldownCut, Is.EqualTo(0.5f));
            Assert.That(After("Backstep", "Forward Step").MoveDistance, Is.EqualTo(5f), "toward the cursor");
            SkillDefinition longStep = After("Backstep", "Long Step");
            Assert.That(longStep.MoveDistance, Is.EqualTo(-8f).Within(1e-5f));
            Assert.That(longStep.RecastWindow, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(After("Backstep", "Second Step").Charges, Is.EqualTo(2));
            // Both Backstep patches together: forward, and 8 m.
            SkillDefinition both = RunSkillUpgrades.Patched(Default("Backstep"), new[] { Row("Backstep", "Long Step"), Row("Backstep", "Forward Step") });
            Assert.That(both.MoveDistance, Is.EqualTo(8f).Within(1e-5f));
            // The pool skills.
            SkillDefinition snap = After("Long Shot", "Snap Shot");
            Assert.That((snap.NoCharge, snap.Cooldown), Is.EqualTo((true, 18f)));
            SkillDefinition over = After("Long Shot", "Overcharge");
            Assert.That(over.PerfectFrom, Is.EqualTo(0.85f).Within(1e-5f));
            Assert.That(over.PerfectTo, Is.EqualTo(0.95f).Within(1e-5f));
            Assert.That(over.PerfectScale, Is.EqualTo(3f));
            Assert.That(After("Powder Keg", "Twin Kegs").Charges, Is.EqualTo(2));
            SkillDefinition big = After("Powder Keg", "Big Keg");
            Assert.That(big.Radius, Is.EqualTo(2.5f * 4f / 3f).Within(1e-4f));
            Assert.That(big.Damage, Is.EqualTo(234));
            Assert.That(After("Fan Fire", "Walking Fire").Lock, Is.EqualTo(SkillLock.Light));
            Assert.That(After("Fan Fire", "Marking Fire").MarkInterval, Is.EqualTo(0.5f));
            SkillDefinition quick = After("Heavy Volley", "Quick Cast");
            Assert.That((quick.Startup, quick.Damage), Is.EqualTo((0.6f, 320)));
            Assert.That(After("Heavy Volley", "Execution").DazedDamageScale, Is.EqualTo(2f));
            Assert.That(After("Tag Round", "Deep Tag").TagDamageShare, Is.EqualTo(0.2f).Within(1e-6f));
            Assert.That(After("Tag Round", "Spreading Tag").TagJump, Is.EqualTo(6f));
            Assert.That(After("Breaker Slug", "Follow Through").DazeCooldownCut, Is.EqualTo(0.5f));
            Assert.That(After("Slide", "Third Wind").Charges, Is.EqualTo(3));
            Assert.That(After("Slide", "Slide Shot").SlideShotMarks, Is.EqualTo(1));
            Assert.That(After("Stopping Shot", "Riposte").CounterReadies, Is.EqualTo("Scatter Blast"));
            Assert.That(After("Stopping Shot", "Wide Counter").CounterHalfAngle, Is.EqualTo(180f));
        }

        [Test]
        public void Exclusions_GoBothWays_AndOnlyBetweenUpgradesOfOneSkill()
        {
            Assert.That(Row("Scatter Blast", "Slug").CannotGoWith(Row("Scatter Blast", "Point Blank")), Is.True);
            Assert.That(Row("Scatter Blast", "Point Blank").CannotGoWith(Row("Scatter Blast", "Slug")), Is.True);
            Assert.That(Row("Long Shot", "Snap Shot").CannotGoWith(Row("Long Shot", "Overcharge")), Is.True);
            Assert.That(Row("Long Shot", "Overcharge").CannotGoWith(Row("Long Shot", "Snap Shot")), Is.True);
            Assert.That(Row("Scatter Blast", "Slug").CannotGoWith(Row("Scatter Blast", "Fresh Shells")), Is.False);
            Assert.That(Row("Quick Shot", "Double Tap").CannotGoWith(Row("Quick Shot", "Quick Draw")), Is.False);

            var had = new HashSet<string> { "Scatter Blast" };
            List<SkillUpgradeDefinition> open = SkillUpgradeOffer.Open(SkillUpgradeTable.Create(), had, new[] { Row("Scatter Blast", "Slug") });
            Assert.That(open.Select(u => u.Name), Is.EquivalentTo(new[] { "Fresh Shells" }), "Slug taken: Point Blank is not offered, nor Slug again");
        }

        // ---------------- The offer ----------------

        static readonly string[] StartFour = { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" };

        [Test]
        public void Offer_ThreeOfSkillsSheHas_DifferentSkills_AChange_TheSameForTheSameSeed()
        {
            SkillUpgradeDefinition[] table = SkillUpgradeTable.Create();
            var had = new HashSet<string>(StartFour);
            var seen = new HashSet<string>();
            for (ulong run = 0; run < 300; run++)
            {
                for (int floor = 1; floor <= 5; floor++)
                {
                    List<SkillUpgradeDefinition> offer = SkillUpgradeOffer.Offer(run, floor, table, had, Array.Empty<SkillUpgradeDefinition>(), 3);
                    Assert.That(offer.Count, Is.EqualTo(3));
                    Assert.That(offer.All(u => had.Contains(u.Skill)), "only skills she has");
                    Assert.That(offer.Select(u => u.Skill).Distinct().Count(), Is.EqualTo(3), "three different skills");
                    Assert.That(offer.Any(u => u.Kind == SkillUpgradeKind.Change), "a change");
                    Assert.That(SkillUpgradeOffer.Offer(run, floor, table, had, Array.Empty<SkillUpgradeDefinition>(), 3).Select(u => u.FullName),
                                Is.EqualTo(offer.Select(u => u.FullName)), "the same seed, the same offer");
                    foreach (SkillUpgradeDefinition u in offer) seen.Add(u.FullName);
                }
            }
            Assert.That(seen.Count, Is.EqualTo(table.Count(u => had.Contains(u.Skill))), "every upgrade of her skills comes up");
        }

        [Test]
        public void Offer_ARunOfPicks_NeverOffersOneTakenOrExcluded_TheSameForTheSamePicks_AndThenHeals()
        {
            SkillUpgradeDefinition[] table = SkillUpgradeTable.Create();
            var had = new HashSet<string>(StartFour);
            for (ulong run = 0; run < 50; run++)
            {
                var taken = new List<SkillUpgradeDefinition>();
                var again = new List<string>();
                for (int floor = 1; floor <= 20; floor++)
                {
                    List<SkillUpgradeDefinition> offer = SkillUpgradeOffer.Offer(run, floor, table, had, taken, 3);
                    List<SkillUpgradeDefinition> open = SkillUpgradeOffer.Open(table, had, taken);
                    Assert.That(offer.Count, Is.EqualTo(Math.Min(3, open.Count)), "fewer left: what is left");
                    foreach (SkillUpgradeDefinition u in offer)
                    {
                        Assert.That(taken.Any(t => t.FullName == u.FullName), Is.False, "never one taken");
                        Assert.That(taken.Any(t => t.CannotGoWith(u)), Is.False, "never one excluded");
                    }
                    if (open.Any(u => u.Kind == SkillUpgradeKind.Change)) Assert.That(offer.Any(u => u.Kind == SkillUpgradeKind.Change));
                    if (open.Select(u => u.Skill).Distinct().Count() >= 3) Assert.That(offer.Select(u => u.Skill).Distinct().Count(), Is.EqualTo(3));
                    if (offer.Count == 0) break;
                    SkillUpgradeDefinition pick = offer[(int)((run + (ulong)floor) % (ulong)offer.Count)];
                    taken.Add(pick);
                    again.Add(pick.FullName);
                }
                Assert.That(SkillUpgradeOffer.Open(table, had, taken), Is.Empty, "in the end nothing is left: the run heals instead");
                // The same seed and the same picks: the same offers.
                var replay = new List<SkillUpgradeDefinition>();
                for (int floor = 1; floor <= again.Count; floor++)
                {
                    List<SkillUpgradeDefinition> offer = SkillUpgradeOffer.Offer(run, floor, table, had, replay, 3);
                    SkillUpgradeDefinition pick = offer.Single(u => u.FullName == again[floor - 1]);
                    replay.Add(pick);
                }
            }
            Assert.That(SkillUpgradeOffer.Offer(1, 1, table, new HashSet<string>(), Array.Empty<SkillUpgradeDefinition>(), 3), Is.Empty, "no skill: nothing");
        }

        // ---------------- The run's view of the skills ----------------

        [Test]
        public void RunView_IsTheSettingsWithNone_CopiesWithSome_AndNeverWritesTheSettings()
        {
            SkillDefinition[] settings = SkillDefaults.Create();
            string before = string.Join("|", settings.Select(Fields));
            var run = new RunSkillUpgrades();
            Assert.That(run.View(settings), Is.SameAs(settings), "no upgrade: the settings' own");
            run.Take(Row("Quick Shot", "Double Tap"));
            run.Take(Row("Quick Shot", "Heavy Round"));
            IReadOnlyList<SkillDefinition> view = run.View(settings);
            Assert.That(view, Is.Not.SameAs(settings));
            Assert.That(view.Count, Is.EqualTo(settings.Length));
            Assert.That(view[0].Charges, Is.EqualTo(2));
            Assert.That(view[0].Damage, Is.EqualTo(160));
            Assert.That(view[4].Damage, Is.EqualTo(settings[4].Damage), "another skill as it is");
            Assert.That(view[0], Is.Not.SameAs(settings[0]));
            Assert.That(run.View(settings), Is.SameAs(view), "made once until something changes");
            Assert.That(run.CountFor("Quick Shot"), Is.EqualTo(2));
            Assert.That(run.NamesFor("Quick Shot"), Is.EqualTo(new[] { "Double Tap", "Heavy Round" }));
            Assert.That(run.Text(), Is.EqualTo("Quick Shot: Double Tap, Quick Shot: Heavy Round"));
            Assert.That(string.Join("|", settings.Select(Fields)), Is.EqualTo(before), "the settings are untouched");
            run.Clear();
            Assert.That(run.View(settings), Is.SameAs(settings), "a new run: none");
            Assert.That(run.Text(), Is.EqualTo("none"));
        }

        // ---------------- The dash's step in the chests ----------------

        [Test]
        public void TheChests_DashStep_CutsTheDashCooldown8Percent_Stacking_NeverUnderTheFloor()
        {
            Assert.That(new RunValues().Upgrades.Count(u => u.Kind == UpgradeKind.DashCooldown && Math.Abs(u.Amount - 0.08f) < 1e-6f), Is.EqualTo(1));
            var m = new RunModifiers();
            Assert.That(m.DashCooldownShare, Is.EqualTo(1f));
            var step = new Upgrade { Kind = UpgradeKind.DashCooldown, Amount = 0.08f };
            m.Take(step);
            Assert.That(m.DashCooldownShare, Is.EqualTo(0.92f).Within(1e-6f));
            m.Take(step);
            Assert.That(m.DashCooldownShare, Is.EqualTo(0.84f).Within(1e-6f), "stacking");
            for (int i = 0; i < 20; i++) m.Take(step);
            Assert.That(m.DashCooldownShare, Is.EqualTo(m.CooldownFloor));
            Assert.That(step.Short, Is.EqualTo("-8% cd dash"));
            Assert.That(step.Text, Is.EqualTo("-8% dash cooldown"));

            var feel = new FeelValues();
            var dash = new Dash();
            Assert.That(dash.TryStart(0f, feel, 0.92f), Is.True);
            Assert.That(dash.CooldownRemaining(0f), Is.EqualTo(feel.DashCooldown * 0.92f).Within(1e-5f));
        }

        // ---------------- The record and the summary ----------------

        [Test]
        public void Record_AndSummary_ListTheSkillUpgradesInOrder_AndTheEliteRooms()
        {
            var record = new RunRecord("ABCDEFGH", 5);
            Assert.That(record.SkillUpgradesText, Is.EqualTo("none"));
            record.TakeSkillUpgrade("Quick Shot: Double Tap");
            record.TakeSkillUpgrade("Backstep: Long Step");
            Assert.That(record.SkillUpgradesText, Is.EqualTo("Quick Shot: Double Tap, Backstep: Long Step"));
            record.EnterFloor(1, 8);
            record.EnterRoom(4, RoomKind.Combat, elite: true);
            Assert.That(record.RoomText, Is.EqualTo("Elite room, 5 of 8"));

            SessionEvent Ev(double t, string type, string name, params string[] extras)
            {
                var e = new SessionEvent(t, type, name);
                for (int i = 0; i + 1 < extras.Length; i += 2) e.AddExtra(extras[i], extras[i + 1]);
                return e;
            }
            var events = new List<SessionEvent>
            {
                Ev(0, SessionEventTypes.SessionStart, null),
                Ev(1, SessionEventTypes.RunStart, "ABCDEFGH"),
                Ev(1, SessionEventTypes.FloorEnter, "22222222", "floor", "1"),
                Ev(10, SessionEventTypes.EliteEnter, null, "floor", "1", "room", "4"),
                Ev(40, SessionEventTypes.EliteClear, null, "floor", "1", "seconds", "30", "health", "70"),
                Ev(40, SessionEventTypes.SkillUpgradeOffer, null, "floor", "1", "offered", "Quick Shot: Double Tap (change) | Backstep: Long Step (numbers)"),
                Ev(40, SessionEventTypes.SkillUpgradeTake, "Quick Shot: Double Tap", "floor", "1", "kind", "change"),
                Ev(90, SessionEventTypes.FloorEnter, "33333333", "floor", "2"),
                Ev(95, SessionEventTypes.EliteEnter, null, "floor", "2", "room", "4"),
                Ev(99, SessionEventTypes.SkillUpgradeTake, "Backstep: Long Step", "floor", "2", "kind", "numbers"),
                Ev(120, SessionEventTypes.SessionEnd, null),
            };
            SessionStats stats = SessionSummary.Compute(events);
            RunRow run = stats.Runs.Single();
            Assert.That(run.SkillUpgrades, Is.EqualTo(new[] { "Quick Shot: Double Tap", "Backstep: Long Step" }));
            Assert.That((run.ElitesEntered, run.ElitesCleared), Is.EqualTo((2, 1)));
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("  skill upgrades   Quick Shot: Double Tap, Backstep: Long Step", text);
            StringAssert.Contains("  elite rooms      entered 2, cleared 1 (30.0 s)", text);
        }
    }
}
