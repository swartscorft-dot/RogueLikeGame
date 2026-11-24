using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 106 (2026-11-24, the boss as a finale), as rules: the break happens once at half its health, whichever blow
    /// crosses it; phase two's tactics, with the Ground Slam one attack in four; the slam's ring and where it lands; Knight's
    /// Resolve opened once by the first kill, saved, read back, and its effect once a run; the session summary's line; and with
    /// the switch off, everything as before. The save is only ever written under a temporary folder of this test.
    /// </summary>
    public class BossFinaleRulesTests
    {
        string _folder;
        bool _finaleBefore;

        [SetUp]
        public void SetUp()
        {
            _finaleBefore = BossValues.Finale;
            BossValues.Finale = true;
            _folder = Path.Combine(Path.GetTempPath(), "BossFinaleRulesTests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            BossValues.Finale = _finaleBefore;
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        string SavePath => Path.Combine(_folder, "between-runs.json");

        static EnemyAttackDefinition Named(EnemyValues v, string name) => v.Attacks.First(a => a.Name == name);

        // ---------------- The switch and the numbers ----------------

        [Test]
        public void TheSwitch_IsOnInCode_AndTheNumbersAreTheProducersStart()
        {
            BossValues.Finale = _finaleBefore;
            Assert.That(BossValues.Finale, Is.True, "on by default");
            var v = new BossValues();
            BossFinaleValues f = v.Phases;
            Assert.That(f.RoarSeconds, Is.EqualTo(1.5f), "about 1.5 s");
            Assert.That(f.Slam.Name, Is.EqualTo(BossFinale.Slam));
            Assert.That(f.Slam.Shape, Is.EqualTo(EnemyAttackShape.Circle), "the ring its Spin uses");
            Assert.That(f.Slam.Radius, Is.EqualTo(4f), "4 m");
            Assert.That(f.Slam.Windup, Is.EqualTo(1f), "about 1 s from crouch to landing");
            Assert.That(f.SlamCrouch, Is.LessThan(f.Slam.Windup), "the crouch is the start of it");
            Assert.That(f.Slam.Counterable, Is.False, "not counterable");
            Assert.That(f.Slam.Rush, Is.EqualTo(0f), "it strikes where it lands, not along a line");
            Assert.That(f.SlamEvery, Is.EqualTo(4), "about one attack in four");
            Assert.That(v.Enemy.Attacks.Select(a => a.Name), Is.EqualTo(new[] { BossValues.Cleave, BossValues.Spin, BossValues.Charge }),
                        "its three attacks as they were: the slam is not among them");
            Assert.That(v.Angry().Attacks.Select(a => a.Name), Is.EqualTo(new[] { BossValues.Cleave, BossValues.Spin, BossValues.Charge }));
            Assert.That(v.Enemy.Health, Is.EqualTo(18000), "its health not changed");
        }

        // ---------------- The break ----------------

        [TestCase(1f, 0.49f, TestName = "TheBreak_FromFullToJustUnderHalf_InOneBlow")]
        [TestCase(0.51f, 0.4999f, TestName = "TheBreak_AHitThatJustCrossesHalf")]
        [TestCase(1f, 0.01f, TestName = "TheBreak_OneBlowFromFullToASliver")]
        [TestCase(0.66f, 0.2f, TestName = "TheBreak_ABlowPastHalfAndAThird")]
        public void TheBreak_HappensOnce_WhicheverBlowCrossesHalf(float before, float after)
        {
            var v = new BossValues();
            var tactics = new BossTactics();
            var phases = new BossPhases();
            Assert.That(tactics.BecomesAngry(before, v), Is.False);
            Assert.That(phases.Breaks(before, v), Is.False, "over half: no break");
            Assert.That(phases.Broken, Is.False);
            Assert.That(tactics.BecomesAngry(after, v), Is.True, "angry, as before");
            Assert.That(phases.Breaks(after, v), Is.True, "the same blow breaks it");
            Assert.That(phases.Broken, Is.True);
            foreach (float later in new[] { after, after * 0.5f, 0.3f, 0f })
                Assert.That(phases.Breaks(later, v), Is.False, "once: " + later);
            Assert.That(phases.Broken, Is.True, "it stays broken");
        }

        [Test]
        public void TheBreak_AtExactlyHalf_NotYet_TheNextPointUnder_Yes()
        {
            var v = new BossValues();
            var phases = new BossPhases();
            Assert.That(phases.Breaks(0.5f, v), Is.False, "half itself is not under half (the anger's own rule)");
            Assert.That(phases.Breaks(8999f / 18000f, v), Is.True, "one point under");
        }

        // ---------------- Phase two's tactics ----------------

        [Test]
        public void BeforeTheBreak_TheChoicesAreTheTacticsOwn_NeverASlam()
        {
            var v = new BossValues();
            v.Phases.SlamEvery = 1; // even were every choice a slam
            var alone = new BossTactics();
            var withPhases = new BossTactics();
            var phases = new BossPhases();
            float[] distances = { 3f, 3f, 2f, 1f, 4.5f, 12f, 20f, 3f, 8f, 3f };
            foreach (float d in distances)
            {
                EnemyAttackDefinition a = alone.Choose(d, true, v.Enemy), b = phases.Choose(withPhases, d, true, v.Enemy, v.Phases);
                Assert.That(b?.Name, Is.EqualTo(a?.Name), "at " + d + " m");
            }
            Assert.That(phases.Choices, Is.EqualTo(0));
            Assert.That(phases.Slams, Is.EqualTo(0));
        }

        [Test]
        public void PhaseTwo_TheSlamIsOneAttackInFour_FromTheFourth_TheOthersTakeTurnsAsBefore()
        {
            var v = new BossValues();
            EnemyValues angry = v.Angry();
            var tactics = new BossTactics();
            var phases = new BossPhases();
            Assert.That(phases.Breaks(0.4f, v), Is.True);
            var names = new List<string>();
            for (int i = 0; i < 16; i++) names.Add(phases.Choose(tactics, 3f, true, angry, v.Phases).Name);
            TestContext.WriteLine(string.Join(", ", names));
            for (int i = 0; i < names.Count; i++)
                Assert.That(names[i] == BossFinale.Slam, Is.EqualTo((i + 1) % 4 == 0), "choice " + (i + 1) + ": " + names[i]);
            List<string> others = names.Where(n => n != BossFinale.Slam).ToList();
            for (int i = 1; i < others.Count; i++) Assert.That(others[i], Is.Not.EqualTo(others[i - 1]), "the cleave and the spin still take turns");
            Assert.That(phases.Slams, Is.EqualTo(4));
            Assert.That(phases.Choices, Is.EqualTo(16));
            Assert.That(names.Where(n => n != BossFinale.Slam).All(n => n == BossValues.Cleave || n == BossValues.Spin));
            EnemyAttackDefinition cleave = phases.Choose(tactics, 4.5f, true, angry, v.Phases);
            Assert.That(cleave.Windup, Is.EqualTo(Named(v.Enemy, BossValues.Cleave).Windup * v.AngerWindup).Within(1e-5f), "phase two's numbers are the angry ones");
        }

        [Test]
        public void PhaseTwo_ASlamThatDoesNotFit_WaitsForTheNextChoiceThatItFits()
        {
            var v = new BossValues();
            EnemyValues angry = v.Angry();
            var tactics = new BossTactics();
            var phases = new BossPhases();
            phases.Breaks(0.3f, v);
            for (int i = 0; i < 3; i++) phases.Choose(tactics, 3f, true, angry, v.Phases);
            Assert.That(phases.SlamDue(v.Phases), Is.True, "the fourth is due");
            Assert.That(phases.Choose(tactics, 15f, true, angry, v.Phases).Name, Is.EqualTo(BossValues.Charge), "15 m: past its leap, a charge");
            Assert.That(phases.Choose(tactics, 10f, false, angry, v.Phases), Is.Null, "10 m, unseen: nothing, it walks (as before)");
            Assert.That(phases.SlamDue(v.Phases), Is.True, "still due");
            Assert.That(phases.Choose(tactics, 4f, false, angry, v.Phases).Name, Is.EqualTo(BossFinale.Slam), "close, sight does not matter");
            Assert.That(phases.SlamDue(v.Phases), Is.False);
            Assert.That(phases.Choose(tactics, 10f, true, angry, v.Phases).Name, Is.EqualTo(BossValues.Charge));
            Assert.That(BossPhases.SlamFits(14f, true, v.Phases), Is.True, "to 14 m");
            Assert.That(BossPhases.SlamFits(14.5f, true, v.Phases), Is.False);
            Assert.That(BossPhases.SlamFits(6f, false, v.Phases), Is.False, "past 5 m it must see her");
        }

        [Test]
        public void TheSwitchOff_NoBreak_NoSlam_TheChoicesAreTodays()
        {
            BossValues.Finale = false;
            var v = new BossValues();
            v.Phases.SlamEvery = 1;
            var tactics = new BossTactics();
            var alone = new BossTactics();
            var phases = new BossPhases();
            Assert.That(phases.Breaks(0.2f, v), Is.False, "off: no break");
            Assert.That(tactics.BecomesAngry(0.2f, v), Is.True, "angry as before");
            EnemyValues angry = v.Angry();
            for (int i = 0; i < 12; i++)
            {
                float d = i % 3 == 0 ? 10f : 3f;
                Assert.That(phases.Choose(tactics, d, true, angry, v.Phases).Name, Is.EqualTo(alone.Choose(d, true, angry).Name), "choice " + i);
            }
            Assert.That(phases.Slams, Is.EqualTo(0));
        }

        // ---------------- The slam's ring ----------------

        [Test]
        public void TheSlam_LandsWhereSheStood_ShortOfAWall_ItsRingIsTheSpinsTest()
        {
            var from = new GroundPoint(0f, 0f);
            var aim = new GroundPoint(0f, 10f);
            GroundPoint open = BossFinale.Landing(from, aim, 10f, 0.7f);
            Assert.That(open.DistanceTo(aim), Is.LessThan(1e-4f), "nothing on the line: where she stood");
            GroundPoint walled = BossFinale.Landing(from, aim, 6f, 0.7f);
            Assert.That(walled.Z, Is.EqualTo(5.3f).Within(1e-4f), "a wall at 6 m: 0.7 m short of it, as its charge");
            Assert.That(walled.X, Is.EqualTo(0f).Within(1e-5f), "on the line");
            Assert.That(BossFinale.Landing(from, aim, 0.2f, 0.7f).DistanceTo(from), Is.LessThan(1e-4f), "never behind where it started");
            Assert.That(BossFinale.Landing(from, from, 0f, 0.7f).DistanceTo(from), Is.LessThan(1e-4f), "she under it: it leaps in place");

            var v = new BossValues();
            float r = v.Phases.Slam.Radius, her = Placeholders.PlayerRadius;
            Assert.That(BossFinale.RingHits(aim, r, aim, her), Is.True, "she stays: hit");
            Assert.That(BossFinale.RingHits(aim, r, new GroundPoint(0f, 10f + r + her - 0.05f), her), Is.True, "her edge in the ring");
            Assert.That(BossFinale.RingHits(aim, r, new GroundPoint(0f, 10f + r + her + 0.05f), her), Is.False, "out of it: missed");
            foreach (float dx in new[] { 0f, 1f, 2.5f, 4.4f, 4.6f, 7f })
            {
                var p = new GroundPoint(dx, 10f);
                Assert.That(BossFinale.RingHits(aim, r, p, her), Is.EqualTo(SkillRules.InCircle(aim, r, p, her)), "the Spin's own test at " + dx);
            }
            float runOut = r + her; // m she must cover from where she stood: in its windup she runs it at her run speed
            Assert.That(runOut / new FeelValues().RunSpeed, Is.LessThan(v.Phases.Slam.Windup), "she can walk out of it in its windup, from its middle");
        }

        [Test]
        public void TheLeap_CrouchesFirst_ThenArcsToTheLanding()
        {
            var f = new BossFinaleValues();
            float w = f.Slam.Windup, c = f.SlamCrouch;
            Assert.That(BossFinale.LeapShare(0f, w, c), Is.EqualTo(0f));
            Assert.That(BossFinale.LeapShare(c / w * 0.99f, w, c), Is.EqualTo(0f), "in the crouch it has not left");
            Assert.That(BossFinale.LeapShare(1f, w, c), Is.EqualTo(1f), "landed as the windup ends");
            Assert.That(BossFinale.LeapShare((c + (w - c) * 0.5f) / w, w, c), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(BossFinale.LeapHeight(0f, f.SlamHeight), Is.EqualTo(0f));
            Assert.That(BossFinale.LeapHeight(1f, f.SlamHeight), Is.EqualTo(0f));
            Assert.That(BossFinale.LeapHeight(0.5f, f.SlamHeight), Is.EqualTo(f.SlamHeight).Within(1e-5f), "its top halfway");
        }

        // ---------------- Knight's Resolve ----------------

        static MetaValues Rack() => KnightsResolve.WithPiece(new MetaValues());

        [Test]
        public void TheRack_HasKnightsResolve_AtItsEnd_NotSold_TwoPoints_FromTheFirstKill()
        {
            MetaValues v = Rack();
            Assert.That(v.Pieces.Length, Is.EqualTo(new MetaValues().Pieces.Length + 1));
            Assert.That(v.Pieces.Take(v.Pieces.Length - 1).Select(p => p.Id), Is.EqualTo(new MetaValues().Pieces.Select(p => p.Id)), "the others as they were, in their order");
            MetaPiece r = v.Pieces.Last();
            Assert.That(r.Id, Is.EqualTo(KnightsResolve.Id));
            Assert.That(r.Name, Is.EqualTo("Knight's Resolve"));
            Assert.That(r.Points, Is.EqualTo(2));
            Assert.That(r.Sold, Is.False, "cannot be bought");
            Assert.That(r.FromFeat, Is.EqualTo(MetaFeat.ActBoss));
            Assert.That(r.Effect, Is.EqualTo(MetaEffect.LastStand));
            Assert.That(KnightsResolve.WithPiece(v).Pieces.Length, Is.EqualTo(v.Pieces.Length), "once");
            Assert.That(MetaRack.WholePrice(v), Is.EqualTo(MetaRack.WholePrice(new MetaValues())), "nothing to buy");
            Assert.That(MetaRack.Buy(new MetaState { Currency = 100000 }, v, KnightsResolve.Id), Is.EqualTo(MetaBuy.NotForSale));
        }

        [Test]
        public void TheSwitchOff_TheRackIsTheTenAsBefore()
        {
            BossValues.Finale = false;
            MetaValues v = Rack();
            Assert.That(v.Pieces.Select(p => p.Id), Is.EqualTo(new MetaValues().Pieces.Select(p => p.Id)));
            Assert.That(MetaRack.Find(v, KnightsResolve.Id), Is.Null);
            var run = new RunEarnings { FloorsCleared = 5, BossKilled = true };
            run.Do(MetaFeat.ActBoss);
            var s = new MetaState();
            MetaRack.Commit(s, v, run);
            Assert.That(s.Owns(KnightsResolve.Id), Is.False);
            Assert.That(KnightsResolve.EndLine(run, v), Is.Null);
            var second = new RunEarnings { FloorsCleared = 5, BossKilled = true };
            second.Do(MetaFeat.ActBoss);
            MetaRack.Commit(s, v, second);
            Assert.That(KnightsResolve.EndLine(second, v), Is.Null, "off: no line on a later kill either");
        }

        [Test]
        public void TheFirstKill_OpensIt_Once_Saved_ReadBack_LaterKillsSayItIsHers()
        {
            MetaValues v = Rack();
            var s = new MetaState();
            Assert.That(s.Owns(KnightsResolve.Id), Is.False);
            var died = new RunEarnings { FloorsCleared = 4 };
            died.Do(MetaFeat.ReachFloor5);
            MetaRack.Commit(s, v, died);
            Assert.That(s.Owns(KnightsResolve.Id), Is.False, "dead before the boss: not opened");
            Assert.That(KnightsResolve.EndLine(died, v), Is.Null);

            var first = new RunEarnings { FloorsCleared = 5, BossKilled = true };
            first.Do(MetaFeat.ActBoss);
            List<MetaFeat> fresh = MetaRack.Commit(s, v, first);
            Assert.That(fresh, Does.Contain(MetaFeat.ActBoss), "the first kill: the feat is new, the end screen's \"New piece\" line names it");
            Assert.That(s.Owns(KnightsResolve.Id), Is.True, "opened");
            Assert.That(s.Owns(MetaPieces.KnightsTrophy), Is.True, "the trophy as before");
            Assert.That(s.Owned.Count(id => id == KnightsResolve.Id), Is.EqualTo(1));
            Assert.That(KnightsResolve.EndLine(first, v), Is.Null, "the first kill's line is the feat's own");
            Assert.That(MetaRack.Equip(s, v, KnightsResolve.Id), Is.EqualTo(MetaEquip.Equipped), "two points of a budget of four");

            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);
            MetaState back = MetaSaveFile.Load(SavePath, v, out MetaLoad how, out _);
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That(back.Owns(KnightsResolve.Id), Is.True, "read back");
            Assert.That(back.IsEquipped(KnightsResolve.Id), Is.True);

            var again = new RunEarnings { FloorsCleared = 5, BossKilled = true };
            again.Do(MetaFeat.ActBoss);
            Assert.That(MetaRack.Commit(back, v, again), Is.Empty, "a later kill: no new feat");
            Assert.That(back.Owned.Count(id => id == KnightsResolve.Id), Is.EqualTo(1), "opened once");
            Assert.That(KnightsResolve.EndLine(again, v), Is.EqualTo("Knight's Resolve is already yours."));

            // Read by a build with the finale off: the piece is not in its rack and is not kept as owned; the feat is, so it comes
            // back as soon as the finale is on again.
            BossValues.Finale = false;
            MetaState off = MetaSaveFile.Load(SavePath, Rack(), out _, out _);
            Assert.That(off.Owns(KnightsResolve.Id), Is.False);
            Assert.That(off.HasFeat(MetaFeat.ActBoss), Is.True);
            BossValues.Finale = true;
            MetaRack.Tidy(off, Rack());
            Assert.That(off.Owns(KnightsResolve.Id), Is.True, "the feat gives it again");
        }

        [Test]
        public void ItsEffect_OnceARun_ABlowThatWouldKillHerLeavesHerAt1()
        {
            MetaValues v = Rack();
            var s = new MetaState();
            s.Feats.Add(MetaFeats.Id(MetaFeat.ActBoss));
            MetaRack.Tidy(s, v);
            var meta = new RunMetaEffects();
            MetaRack.Apply(s, v, meta);
            Assert.That(meta.Resolves, Is.EqualTo(0), "owned but not equipped: nothing");
            Assert.That(KnightsResolve.Spare(500, 20, meta, out bool spared), Is.EqualTo(500));
            Assert.That(spared, Is.False);

            Assert.That(MetaRack.Equip(s, v, KnightsResolve.Id), Is.EqualTo(MetaEquip.Equipped));
            MetaRack.Apply(s, v, meta);
            Assert.That(meta.Resolves, Is.EqualTo(1));
            Assert.That(meta.ResolvesLeft, Is.EqualTo(1));
            Assert.That(meta.ResolveReach, Is.EqualTo(4f), "within 4 m");
            Assert.That(meta.Pieces, Is.EqualTo(new[] { "Knight's Resolve" }));
            Assert.That(KnightsResolve.Spare(10, 20, meta, out spared), Is.EqualTo(10), "a blow she lives through: as it is");
            Assert.That(spared, Is.False);
            Assert.That(meta.ResolvesLeft, Is.EqualTo(1), "not spent on it");
            Assert.That(KnightsResolve.Spare(45, 20, meta, out spared), Is.EqualTo(19), "a killing blow: she keeps 1");
            Assert.That(spared, Is.True);
            Assert.That(meta.ResolvesLeft, Is.EqualTo(0));
            Assert.That(KnightsResolve.Spare(20, 20, meta, out spared), Is.EqualTo(20), "once a run: the next one kills her");
            Assert.That(spared, Is.False);
            Assert.That(KnightsResolve.Spare(45, 1, new RunMetaEffects { Resolves = 1, ResolvesLeft = 1 }, out spared), Is.EqualTo(0), "at 1 already: nothing taken");
            Assert.That(spared, Is.True);

            MetaRack.Apply(s, v, meta);
            Assert.That(meta.ResolvesLeft, Is.EqualTo(1), "the next run has it again");
            meta.Clear();
            Assert.That(meta.Resolves + meta.ResolvesLeft, Is.EqualTo(0));
            Assert.That(meta.ResolveReach, Is.EqualTo(0f));
            Assert.That(KnightsResolve.Spare(45, 20, null, out spared), Is.EqualTo(45), "no run: as it is");
        }

        [Test]
        public void NothingEquipped_TheRunsEffectsAreAsBefore()
        {
            MetaValues v = Rack();
            var s = new MetaState { Currency = 100000 };
            foreach (MetaFeat f in MetaFeats.All) s.Feats.Add(MetaFeats.Id(f));
            MetaRack.Tidy(s, v);
            var with = new RunMetaEffects();
            MetaRack.Apply(s, v, with);
            var plain = new RunMetaEffects();
            Assert.That(with.ToString(), Is.EqualTo(plain.ToString()));
            Assert.That(with.Resolves, Is.EqualTo(0));
            Assert.That(with.Any, Is.False);
        }

        // ---------------- The session summary ----------------

        static SessionEvent Fight(double t, string one, string two, bool broke, int slams, int landed, int avoided, string ended)
        {
            var e = new SessionEvent(t, BossFightEvents.Fight);
            e.AddExtra("one", one);
            e.AddExtra("two", two);
            e.AddExtra("broke", broke ? "1" : "0");
            e.AddExtra("slams", slams.ToString());
            e.AddExtra("landed", landed.ToString());
            e.AddExtra("avoided", avoided.ToString());
            e.AddExtra("ended", ended);
            return e;
        }

        [Test]
        public void TheSummary_TellsTheBossFightInOneLine_AndNothingWithoutIt()
        {
            var events = new List<SessionEvent>
            {
                new SessionEvent(0, SessionEventTypes.SessionStart),
                Fight(100, "40.0", "0.0", false, 0, 0, 0, BossFightEvents.HerDeath),
                Fight(300, "62.0", "48.3", true, 5, 1, 4, BossFightEvents.Died),
                new SessionEvent(400, SessionEventTypes.SessionEnd),
            };
            foreach (SessionEvent e in events)
            {
                SessionEvent back = SessionEvent.Parse(e.ToJson());
                Assert.That(back.Type, Is.EqualTo(e.Type));
                Assert.That(back.ExtraValue("two"), Is.EqualTo(e.ExtraValue("two")));
            }
            List<BossFightRow> rows = SessionSummary.ComputeBossFights(events);
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[1].PhaseOne, Is.EqualTo(62d).Within(1e-9));
            Assert.That(rows[1].PhaseTwo, Is.EqualTo(48.3d).Within(1e-9));
            Assert.That(rows[1].Landed, Is.EqualTo(1));
            Assert.That(rows[1].Avoided, Is.EqualTo(4));
            string text = SessionSummary.Format(SessionSummary.Compute(events));
            TestContext.WriteLine(text);
            string line = text.Split('\n').Select(l => l.TrimEnd('\r')).Single(l => l.StartsWith("Boss ", StringComparison.Ordinal));
            Assert.That(line, Is.EqualTo("Boss 2 fights: phase one 40.0 s, no break, Ground Slams 0 (landed 0, avoided 0), her death;  "
                                         + "phase one 62.0 s, phase two 48.3 s, Ground Slams 5 (landed 1, avoided 4), died"));
            string without = SessionSummary.Format(SessionSummary.Compute(events.Where(e => e.Type != BossFightEvents.Fight).ToList()));
            Assert.That(without.Split('\n').Any(l => l.StartsWith("Boss ", StringComparison.Ordinal)), Is.False, "nothing for a session without the lines (the finale off, or a log from before)");
        }

        // ---------------- The roar ----------------

        [Test]
        public void TheRoar_IsASoundAtTheEnd_TheBreaksMomentGivesIt_AsLoudAsTheBossesOthers()
        {
            Assert.That((int)PlaceholderSound.BossRoar, Is.EqualTo(43), "added after the thirty-eight and the five of work order 113; other orders of the day sprint follow it (changed at the merge, 2026-11-24)");
            Assert.That((int)PlaceholderSound.OffTick, Is.EqualTo(37), "the others where they were");
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.BossRoared), Is.EqualTo(PlaceholderSound.BossRoar));
            var values = new PlaceholderSoundValues();
            Assert.That(values.Volume(PlaceholderSound.BossRoar), Is.EqualTo(values.Volume(PlaceholderSound.BossAnger)));
            float[] roar = Synth.Render(PlaceholderRecipes.For(PlaceholderSound.BossRoar), Synth.SampleRate);
            float[] growl = Synth.Render(PlaceholderRecipes.For(PlaceholderSound.BossAnger), Synth.SampleRate);
            Assert.That(roar.Length, Is.GreaterThan(growl.Length), "longer than the anger's growl, which it follows");
            Assert.That(PlaceholderRecipes.Seconds(PlaceholderSound.BossRoar), Is.LessThanOrEqualTo(new BossFinaleValues().RoarSeconds), "over within the roar");
        }
    }
}
