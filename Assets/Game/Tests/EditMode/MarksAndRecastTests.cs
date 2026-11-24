using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>Decision 0005 (2026-11-19): marks on a target, how they are added, capped, lost and taken.</summary>
    public class MarksTests
    {
        const int Max = 5;
        const float Life = 6f;

        [Test]
        public void Added_CountUp_ToTheCap()
        {
            var marks = new Marks();
            Assert.That(marks.Count(0f), Is.EqualTo(0));
            Assert.That(marks.Add(1, 0f, Max, Life), Is.EqualTo(1));
            Assert.That(marks.Add(2, 0.5f, Max, Life), Is.EqualTo(3));
            Assert.That(marks.Add(4, 1f, Max, Life), Is.EqualTo(Max), "never more than the cap");
            Assert.That(marks.Count(1f), Is.EqualTo(Max));
        }

        [Test]
        public void TheyAllFallOff_ALifetimeAfterTheLastWasAdded()
        {
            var marks = new Marks();
            marks.Add(1, 0f, Max, Life);
            marks.Add(1, 4f, Max, Life); // the lifetime starts again here
            Assert.That(marks.Count(9.9f), Is.EqualTo(2), "the first mark is older than the lifetime, but the last is not");
            Assert.That(marks.Count(10f), Is.EqualTo(0), "all at once, a lifetime after the last");
            Assert.That(marks.Add(1, 11f, Max, Life), Is.EqualTo(1), "and they start again from none");
        }

        [Test]
        public void FallenOff_AreCountedOnce()
        {
            var marks = new Marks();
            marks.Add(3, 0f, Max, Life);
            Assert.That(marks.TakeFallenOff(5f), Is.EqualTo(0), "not yet");
            Assert.That(marks.TakeFallenOff(6f), Is.EqualTo(3));
            Assert.That(marks.TakeFallenOff(7f), Is.EqualTo(0), "counted once");
        }

        [Test]
        public void ASpender_TakesThemAll()
        {
            var marks = new Marks();
            marks.Add(4, 0f, Max, Life);
            Assert.That(marks.TakeAll(1f), Is.EqualTo(4));
            Assert.That(marks.Count(1f), Is.EqualTo(0));
            Assert.That(marks.TakeAll(1f), Is.EqualTo(0));
            Assert.That(marks.TakeFallenOff(100f), Is.EqualTo(0), "taken marks do not fall off as well");
        }

        [Test]
        public void Cleared_LeavesNone()
        {
            var marks = new Marks();
            marks.Add(2, 0f, Max, Life);
            Assert.That(marks.Clear(1f), Is.EqualTo(2));
            Assert.That(marks.Count(1f), Is.EqualTo(0));
        }

        [Test]
        public void SpenderDamage_IsTheBonusPerMarkTaken()
        {
            Assert.That(MarkRules.SpenderDamage(220, 0, 0.25f), Is.EqualTo(220));
            Assert.That(MarkRules.SpenderDamage(220, 1, 0.25f), Is.EqualTo(275));
            Assert.That(MarkRules.SpenderDamage(220, 5, 0.25f), Is.EqualTo(495), "+125 % at five");
            Assert.That(MarkRules.SpenderDamage(400, 5, 0.4f), Is.EqualTo(1200));
        }

        [Test]
        public void TheBasicShot_MarksEveryFourthHit()
        {
            var count = new BasicShotMarks();
            bool[] marked = Enumerable.Range(0, 8).Select(_ => count.Hit(4, everyHit: false)).ToArray();
            Assert.That(marked, Is.EqualTo(new[] { false, false, false, true, false, false, false, true }));
        }

        [Test]
        public void TheBasicShot_MarksEveryHit_InTheFocusWindow_AndTheCountStands()
        {
            var count = new BasicShotMarks();
            count.Hit(4, false);
            count.Hit(4, false);
            Assert.That(count.Hit(4, everyHit: true), Is.True);
            Assert.That(count.Hit(4, everyHit: true), Is.True);
            Assert.That(count.Counted, Is.EqualTo(2), "the count stands");
            Assert.That(count.Hit(4, false), Is.False);
            Assert.That(count.Hit(4, false), Is.True);
        }

        /// <summary>Changed deliberately 2026-11-19 after Kristian played: marks build a little slower, 5 basic shot hits a mark (was 4).</summary>
        [Test]
        public void TheKitsStartValues()
        {
            var kit = new KitValues();
            Assert.That(kit.MaxMarks, Is.EqualTo(5));
            Assert.That(kit.MarkLifetime, Is.EqualTo(6f));
            Assert.That(kit.BasicShotsPerMark, Is.EqualTo(5));
            Assert.That(kit.StartWithAllSkills, Is.False);
            Assert.That(kit.CleanShare, Is.EqualTo(1f / 3f).Within(1e-6f));
            Assert.That(kit.CleanDamage, Is.EqualTo(1.75f)); // work order 064: up from 1.5
        }

        [Test]
        public void FourStartSkills_TwoLight_OneSpenderAmongThem()
        {
            SkillDefinition[] skills = SkillDefaults.Create();
            Assert.That(skills.Where(s => s.StartEquipped).Select(s => s.Name),
                        Is.EquivalentTo(new[] { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" }));
            Assert.That(skills.Where(s => s.StartEquipped && s.Lock == SkillLock.Light).Select(s => s.Name), Is.EquivalentTo(new[] { "Quick Shot", "Triple Tap" }));
            // Work order 015: Powder Keg became light, and three of the pool's new four are.
            Assert.That(skills.Where(s => !s.StartEquipped && s.Lock == SkillLock.Light).Select(s => s.Name),
                        Is.EquivalentTo(new[] { "Powder Keg", "Tag Round", "Slide", "Stopping Shot", "Ricochet", "Smoke Round" })); // changed deliberately (work order 109): two of its three are light
            Assert.That(skills.Where(MarkRules.IsSpender).Select(s => s.Name), Is.EquivalentTo(new[] { "Scatter Blast", "Heavy Volley", "Deadeye" })); // and Deadeye spends (work order 109)
            Assert.That(skills.Where(s => s.MarksAdded > 0 && MarkRules.IsSpender(s)), Is.Empty, "a spender adds no marks");
        }

        // ---------------- Backstep's clean spend (amended 2026-11-19 after Kristian played) ----------------

        [Test]
        public void TheStartValues_BackstepIsTwoPresses_ThatOpenWithTheHop_AndScatterBlastIsBackSooner()
        {
            SkillDefinition[] skills = SkillDefaults.Create();
            SkillDefinition step = skills.Single(s => s.Name == "Backstep");
            Assert.That(step.RecastCount, Is.EqualTo(2));
            Assert.That(step.RecastWindow, Is.EqualTo(0.6f));
            Assert.That(step.OpeningMove, Is.True);
            Assert.That(step.CleanMarkDamageBonus, Is.EqualTo(0.5f));
            Assert.That(step.MoveDistance, Is.EqualTo(-5f), "the hop as before");
            Assert.That(step.MarksAdded, Is.EqualTo(1), "an ordinary shot still marks");
            Assert.That(skills.Single(s => s.Name == "Scatter Blast").Cooldown, Is.EqualTo(6f), "8 s before");
            Assert.That(skills.Where(s => s.Name != "Backstep").Select(s => s.CleanMarkDamageBonus), Is.All.EqualTo(0f), "no other skill has a clean spend");
            Assert.That(skills.Where(s => s.Name != "Backstep").Select(s => s.OpeningMove), Is.All.False, "nor an opening move");
            Assert.That(skills.Single(s => s.Name == "Triple Tap").CleanTapMarks, Is.EqualTo(2), "Triple Tap's clean tap as it was");
        }

        [Test]
        public void ACleanSpend_TakesTheMarks_ForItsOwnBonusPerMark_OnlyOnACleanPress()
        {
            var step = new SkillDefinition { Damage = 80, MarksAdded = 1, CleanMarkDamageBonus = 0.5f };
            Assert.That(MarkRules.IsSpender(step), Is.False, "not a spender by itself");
            Assert.That(MarkRules.Spends(step, clean: false), Is.False, "an ordinary press spends nothing");
            Assert.That(MarkRules.Spends(step, clean: true), Is.True, "a clean press spends");
            Assert.That(MarkRules.BonusPerMark(step, clean: true), Is.EqualTo(0.5f));
            Assert.That(MarkRules.IsCleanTap(step, clean: true), Is.False, "and is no clean tap: no clean damage on top");
            Assert.That(MarkRules.SpenderDamage(step.Damage, 5, MarkRules.BonusPerMark(step, true)), Is.EqualTo(280), "+50 % a mark: +250 % at five");
            Assert.That(MarkRules.SpenderDamage(step.Damage, 2, MarkRules.BonusPerMark(step, true)), Is.EqualTo(160));
            Assert.That(MarkRules.SpenderDamage(step.Damage, 0, MarkRules.BonusPerMark(step, true)), Is.EqualTo(80), "no marks: the base damage");

            var tap = new SkillDefinition { Damage = 70, MarksAdded = 1, CleanTapMarks = 2 };
            Assert.That(MarkRules.Spends(tap, clean: true), Is.False, "Triple Tap's clean press stays a clean tap");
            Assert.That(MarkRules.IsCleanTap(tap, clean: true), Is.True);
            Assert.That(MarkRules.IsCleanTap(tap, clean: false), Is.False);
            var blast = new SkillDefinition { Damage = 220, MarkDamageBonus = 0.25f };
            Assert.That(MarkRules.Spends(blast, clean: false), Is.True, "a spender spends on every hit");
            Assert.That(MarkRules.BonusPerMark(blast, clean: false), Is.EqualTo(0.25f));
        }
    }

    /// <summary>Decision 0005: spending focus cuts every running cooldown by a share of what is left.</summary>
    public class CooldownCutTests
    {
        [Test]
        public void ACut_TakesTheShareOfWhatIsLeft()
        {
            var cooldown = new Cooldown();
            cooldown.Start(0f, 10f);
            Assert.That(cooldown.Cut(5f, 0.4f), Is.EqualTo(2f).Within(1e-5f), "5 s left: 2 s cut");
            Assert.That(cooldown.Remaining(5f), Is.EqualTo(3f).Within(1e-5f));
        }

        [Test]
        public void AReadyCooldown_IsLeftAlone()
        {
            var cooldown = new Cooldown();
            Assert.That(cooldown.Cut(1f, 0.4f), Is.EqualTo(0f));
            Assert.That(cooldown.IsReady(1f), Is.True);
        }

        [Test]
        public void TheDashsCooldown_IsCutTheSameWay()
        {
            var dash = new Dash();
            Assert.That(dash.TryStart(0f, new FeelValues { DashCooldown = 2f }), Is.True);
            Assert.That(dash.CutCooldown(1f, 0.4f), Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(dash.CooldownRemaining(1f), Is.EqualTo(0.6f).Within(1e-5f));
        }

        [Test]
        public void FocusFromAnySource_StopsAtFull_AndNoneWhileEmpowered()
        {
            var values = new IdentityValues();
            var gauge = new FocusGauge();
            Assert.That(gauge.Add(values.FocusPerMarkSpent * 5, values, 0f), Is.EqualTo(25f));
            Assert.That(gauge.AddDamage(1000, values, 0f), Is.EqualTo(5f).Within(1e-4f), "a trickle: 0.005 a point");
            Assert.That(gauge.Add(200f, values, 0f), Is.EqualTo(70f).Within(1e-4f), "never more than full");
            Assert.That(gauge.TryActivate(0f, values), Is.True);
            Assert.That(gauge.Add(5f, values, 1f), Is.EqualTo(0f), "none while the window runs");
        }
    }

    /// <summary>Decision 0005: a skill pressed several times, one shot a press, the last one clean late in its window.</summary>
    public class RecastChainTests
    {
        const float Window = 0.6f;
        const float Share = 1f / 3f;

        [Test]
        public void ACountOfOne_IsNoChain()
        {
            var chain = new RecastChain();
            chain.Start(0, 1, Window);
            Assert.That(chain.Slot, Is.EqualTo(-1));
            Assert.That(chain.Judge(0, 0.5f, Share, true), Is.EqualTo(RecastPress.None));
        }

        [Test]
        public void BeforeTheShotHasGoneOff_APressIsEarly()
        {
            var chain = new RecastChain();
            chain.Start(7, 3, Window);
            Assert.That(chain.Judge(7, 0.1f, Share, true), Is.EqualTo(RecastPress.Early));
            Assert.That(chain.Judge(3, 0.1f, Share, true), Is.EqualTo(RecastPress.None), "another key is no recast");
        }

        [Test]
        public void ThreePresses_InTheirWindows_TheLastCleanOnlyLate()
        {
            var chain = new RecastChain();
            chain.Start(7, 3, Window);
            chain.ShotWentOff(0.15f);
            // The second press is never clean, however late.
            Assert.That(chain.Judge(7, 0.7f, Share, true), Is.EqualTo(RecastPress.Shot));
            chain.Recast();
            Assert.That(chain.Shots, Is.EqualTo(2));
            Assert.That(chain.Judge(7, 0.75f, Share, true), Is.EqualTo(RecastPress.Early), "its window opens when its shot goes off");
            chain.ShotWentOff(0.85f); // the window runs to 1.45; its clean part from 1.25
            Assert.That(chain.NextIsLast, Is.True);
            Assert.That(chain.Judge(7, 1.0f, Share, true), Is.EqualTo(RecastPress.Shot), "early in the window: an ordinary shot");
            Assert.That(chain.Judge(7, 1.3f, Share, true), Is.EqualTo(RecastPress.Clean), "in the last third: clean");
            Assert.That(chain.Judge(7, 1.3f, Share, false), Is.EqualTo(RecastPress.Shot), "a skill with no clean tap");
            Assert.That(chain.Judge(7, 1.5f, Share, true), Is.EqualTo(RecastPress.None), "late: the window has run out");
            chain.Recast();
            chain.ShotWentOff(1.45f);
            Assert.That(chain.IsComplete, Is.True);
            Assert.That(chain.End(), Is.EqualTo(3));
        }

        [Test]
        public void ARunOutWindow_EndsTheChain_ShortOfItsCount()
        {
            var chain = new RecastChain();
            chain.Start(7, 3, Window);
            chain.ShotWentOff(0.15f);
            Assert.That(chain.Expired(0.7f), Is.False);
            Assert.That(chain.Expired(0.76f), Is.True);
            Assert.That(chain.End(), Is.EqualTo(1));
            Assert.That(chain.Slot, Is.EqualTo(-1));
        }

        [Test]
        public void TheWindowLeft_RunsFromOneToZero()
        {
            var chain = new RecastChain();
            chain.Start(7, 3, Window);
            Assert.That(chain.WindowLeft(0.1f), Is.EqualTo(0f), "no window before the shot goes off");
            chain.ShotWentOff(0f);
            Assert.That(chain.WindowLeft(0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(chain.WindowLeft(0.3f), Is.EqualTo(0.5f).Within(1e-5f));
        }

        /// <summary>
        /// Amended 2026-11-19 (Backstep): a chain that opens with a move. The first press is the move and fires nothing;
        /// its window opens when the move ends; the second press is the last, and may be clean.
        /// </summary>
        [Test]
        public void AChainThatOpensWithAMove_TheFirstPressFiresNothing_TheSecondIsTheShot()
        {
            var chain = new RecastChain();
            chain.Start(6, 2, Window, openingMove: true);
            Assert.That(chain.Slot, Is.EqualTo(6));
            Assert.That(chain.IsOpeningMove, Is.True, "the first press is the move");
            Assert.That(chain.ShotsFired, Is.EqualTo(0), "and fires nothing");
            Assert.That(chain.Judge(6, 0.1f, Share, true), Is.EqualTo(RecastPress.Early), "during the move: no window yet");
            chain.ShotWentOff(0.2f); // the hop ends: the window runs to 0.8; its clean part from 0.6
            Assert.That(chain.IsWindowOpen(0.2f), Is.True);
            Assert.That(chain.NextIsLast, Is.True, "the next press is the last");
            Assert.That(chain.IsComplete, Is.False, "the move is no shot: the chain waits");
            Assert.That(chain.Judge(6, 0.3f, Share, true), Is.EqualTo(RecastPress.Shot), "early in the window: an ordinary shot");
            Assert.That(chain.Judge(6, 0.65f, Share, true), Is.EqualTo(RecastPress.Clean), "in the last third: clean");
            Assert.That(chain.Judge(6, 0.85f, Share, true), Is.EqualTo(RecastPress.None), "after the window: nothing");
            chain.Recast();
            Assert.That(chain.IsOpeningMove, Is.False, "the second press is the shot");
            Assert.That(chain.ShotsFired, Is.EqualTo(1));
            chain.ShotWentOff(0.85f);
            Assert.That(chain.IsComplete, Is.True);
            Assert.That(chain.End(), Is.EqualTo(2), "two presses");
        }

        [Test]
        public void AChainThatOpensWithAMove_WithNoSecondPress_EndsWithNoShot()
        {
            var chain = new RecastChain();
            chain.Start(6, 2, Window, openingMove: true);
            chain.ShotWentOff(0.2f);
            Assert.That(chain.Expired(0.79f), Is.False);
            Assert.That(chain.Expired(0.81f), Is.True, "the window ran out");
            Assert.That(chain.ShotsFired, Is.EqualTo(0));
            Assert.That(chain.End(), Is.EqualTo(1), "one press: the move");
        }

        [Test]
        public void AnOpeningMove_OnASkillWithACountOfOne_IsAnOrdinaryUse()
        {
            var chain = new RecastChain();
            chain.Start(6, 1, Window, openingMove: true);
            Assert.That(chain.Slot, Is.EqualTo(-1));
            Assert.That(chain.IsOpeningMove, Is.False, "no chain, no opening move: its one press fires");
        }

        [Test]
        public void ACleanPress_IsForSkillsWithACleanTapOrACleanSpend()
        {
            Assert.That(RecastChain.HasCleanPress(new SkillDefinition { CleanTapMarks = 2 }), Is.True);
            Assert.That(RecastChain.HasCleanPress(new SkillDefinition { CleanMarkDamageBonus = 0.5f }), Is.True);
            Assert.That(RecastChain.HasCleanPress(new SkillDefinition()), Is.False);
        }

        [Test]
        public void Clean_IsTheLastShareOfTheWindow()
        {
            Assert.That(RecastChain.IsClean(0.39f, Window, Share), Is.False);
            Assert.That(RecastChain.IsClean(0.4f, Window, Share), Is.True);
            Assert.That(RecastChain.IsClean(0.6f, Window, Share), Is.True);
            Assert.That(RecastChain.IsClean(0.61f, Window, Share), Is.False);
        }
    }

    /// <summary>
    /// Work order 038 (Kristian's answer of 2026-11-20: the recast window runs on real time). Which seconds an open window
    /// counts: a hit stop's real seconds do, slowed with a slow-down and never sped up; a pause does not; a frame that ran counts its game time.
    /// </summary>
    public class RecastClockTests
    {
        const float Window = 0.6f;
        const float Share = 1f / 3f;
        const float Frame = 0.0167f;

        [Test]
        public void TheSwitch_IsOnByDefault()
        {
            Assert.That(new KitValues().RecastOnRealTime, Is.True);
        }

        [Test]
        public void AFrameAHitStopFroze_CountsItsRealSeconds()
        {
            Assert.That(RecastClock.HitStopSeconds(true, 0f, Frame, hitStopHolds: true, screenHolds: false, paceBefore: 1f),
                        Is.EqualTo(Frame).Within(1e-7f));
        }

        [Test]
        public void APause_StopsIt_EvenOverAHitStop()
        {
            Assert.That(RecastClock.HitStopSeconds(true, 0f, Frame, hitStopHolds: false, screenHolds: true, paceBefore: 1f), Is.EqualTo(0f), "the pause menu, a choice, a screen");
            Assert.That(RecastClock.HitStopSeconds(true, 0f, Frame, hitStopHolds: true, screenHolds: true, paceBefore: 1f), Is.EqualTo(0f), "paused during a hit stop");
            Assert.That(RecastClock.HitStopSeconds(true, 0f, Frame, hitStopHolds: false, screenHolds: false, paceBefore: 1f), Is.EqualTo(0f), "a time scale of 0 that is no hit stop");
        }

        [Test]
        public void AFrameThatRan_AddsNothing_ItsGameTimeIsTheWindowsAlready()
        {
            Assert.That(RecastClock.HitStopSeconds(true, Frame, Frame, hitStopHolds: true, screenHolds: false, paceBefore: 1f), Is.EqualTo(0f),
                        "the frame the hit stop began in ran");
            Assert.That(RecastClock.HitStopSeconds(true, Frame * 0.5f, Frame, hitStopHolds: false, screenHolds: false, paceBefore: 0.5f), Is.EqualTo(0f),
                        "a slow-down: its game seconds, slowed, and nothing more");
        }

        [Test]
        public void AHitStop_IsSlowedWithASlowDown_AndAtAFasterGameCostsTheWindowWhatItCostsAtScale1()
        {
            Assert.That(RecastClock.HitStopSeconds(true, 0f, Frame, true, false, paceBefore: 0.5f), Is.EqualTo(Frame * 0.5f).Within(1e-7f), "a slow-down slows it");
            // Changed after the evening's merge (work order 038): it was Frame * 8, the faster game's pace. A hit stop is
            // its real length at any scale; unscaled, it costs the window the same share of itself as at scale 1.
            Assert.That(RecastClock.HitStopSeconds(true, 0f, Frame, true, false, paceBefore: 4f), Is.EqualTo(Frame).Within(1e-7f), "at 4: its real seconds, unscaled");
            Assert.That(RecastClock.HitStopSeconds(true, 0f, Frame, true, false, paceBefore: 8f), Is.EqualTo(Frame).Within(1e-7f), "at 8 too");
            Assert.That(RecastClock.HitStopSeconds(true, 0f, Frame, true, false, paceBefore: 0f), Is.EqualTo(0f), "a hit stop that began paused");
            // The share of Triple Tap's window (0.6 s of game time) a hit stop of 0.05 s of real time costs, at 1 and at 4.
            float atOne = RecastClock.HitStopSeconds(true, 0f, 0.05f, true, false, 1f) / Window;
            float atFour = RecastClock.HitStopSeconds(true, 0f, 0.05f, true, false, 4f) / Window;
            Assert.That(atFour, Is.EqualTo(atOne).Within(1e-6f), "the same share at a faster game");
        }

        [Test]
        public void Off_NothingCounts()
        {
            Assert.That(RecastClock.HitStopSeconds(false, 0f, Frame, true, false, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void AWindowRunOnThroughAHitStop_IsCleanAtTheSameRealMoment()
        {
            // The window opens at 1.0 game time; a hit stop of 0.2 s of real time lands at 1.05 (game time stands at 1.05
            // for it). A press 0.5 s of real time after the window opened is at 1.3 game time.
            RecastChain Open()
            {
                var chain = new RecastChain();
                chain.Start(7, 2, Window);
                chain.ShotWentOff(1f);
                return chain;
            }
            RecastChain none = Open(), ranOn = Open(), asBefore = Open();
            ranOn.RunOn(0.2f);
            Assert.That(none.Judge(7, 1.5f, Share, true), Is.EqualTo(RecastPress.Clean), "no hit stop: 0.5 s in, clean");
            Assert.That(ranOn.Judge(7, 1.3f, Share, true), Is.EqualTo(RecastPress.Clean), "run on: clean at the same real moment");
            Assert.That(asBefore.Judge(7, 1.3f, Share, true), Is.EqualTo(RecastPress.Shot), "held as before: 0.3 s in, not clean");
            Assert.That(ranOn.SinceOpened(1.3f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(ranOn.WindowLeft(1.3f), Is.EqualTo(none.WindowLeft(1.5f)).Within(1e-5f), "the bar the same");
            Assert.That(ranOn.Judge(7, 1.05f, Share, true), Is.EqualTo(RecastPress.Shot), "early stays early: 0.25 s in");
            Assert.That(ranOn.Expired(1.39f), Is.False);
            Assert.That(ranOn.Expired(1.41f), Is.True, "it runs out 0.6 s of real time after it opened");
            Assert.That(ranOn.IsWindowOpen(1.41f), Is.False);
            Assert.That(asBefore.Expired(1.41f), Is.False, "held, it ran out 0.2 s later");
        }

        [Test]
        public void BeforeItOpens_NothingRunsOn_AndTheNextWindowStartsFromNothing()
        {
            var chain = new RecastChain();
            chain.Start(7, 3, Window);
            chain.RunOn(0.3f); // a hit stop before its shot has gone off
            Assert.That(chain.Judge(7, 0.1f, Share, true), Is.EqualTo(RecastPress.Early), "still early");
            chain.ShotWentOff(0.2f);
            Assert.That(chain.SinceOpened(0.2f), Is.EqualTo(0f), "its clock starts when it opens");
            chain.RunOn(0.1f);
            Assert.That(chain.SinceOpened(0.3f), Is.EqualTo(0.2f).Within(1e-5f));
            chain.Recast();
            chain.ShotWentOff(0.5f);
            Assert.That(chain.SinceOpened(0.5f), Is.EqualTo(0f), "the next window starts from nothing");
            chain.End();
            chain.RunOn(0.1f);
            Assert.That(chain.SinceOpened(1f), Is.EqualTo(-1f), "no chain, no clock");
        }
    }
}
