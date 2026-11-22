using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 069 (Kristian, 2026-11-21 evening: "høyreklik er litt klunky. jeg vil at de to første skal kome relativt
    /// fort, og det 3. litt senere"), as rules: each press of a recast chain has its own window and clean part
    /// (<see cref="RecastRhythm"/>): Triple Tap's presses before the last quick (open to 0.45 s, clean 0.12 to 0.30 s after the
    /// shot before), its last a beat later (open to 0.80 s, clean 0.42 to 0.66 s); the one function at its edges; Wide window
    /// widening each press round its middle inside its own window; the line of a chain (<see cref="RecastLine"/>) with how
    /// each press fell; and Backstep, the switch off, the cooldown buffer and the charges as they were.
    /// </summary>
    public class TripleTapRhythmRulesTests
    {
        const int Slot = 7;

        static SkillDefinition Skill(string name) => SkillDefaults.Create().First(s => s.Name == name);

        // ---------------- 1. Each press its own window and clean part ----------------

        [Test]
        public void TheKit_HoldsTheRhythm_QuickPressesThenABeat_WithTheSwitchOn()
        {
            var kit = new KitValues();
            Assert.That(kit.ChainRhythm, Is.True, "the rhythm as the game ships");
            Assert.That(kit.QuickPressWindow, Is.EqualTo(0.45f));
            Assert.That(kit.QuickPressCleanFrom, Is.EqualTo(0.12f));
            Assert.That(kit.QuickPressCleanTo, Is.EqualTo(0.30f));
            Assert.That(kit.LastPressWindow, Is.EqualTo(0.80f));
            Assert.That(kit.LastPressCleanFrom, Is.EqualTo(0.42f));
            Assert.That(kit.LastPressCleanTo, Is.EqualTo(0.66f));
            Assert.That(kit.LastPressCleanFrom, Is.GreaterThan(kit.QuickPressWindow - 0.05f), "the beat: the last press's clean part lies past where a quick press's window has nearly closed");
        }

        [Test]
        public void TripleTap_TheSecondPressIsQuick_TheThirdABeatLater()
        {
            var kit = new KitValues();
            SkillDefinition tap = Skill("Triple Tap");
            Assert.That(tap.RecastCount, Is.EqualTo(3));
            RecastBeat second = RecastRhythm.Press(kit, tap, 2, 0f), third = RecastRhythm.Press(kit, tap, 3, 0f);
            Assert.That(second.Press, Is.EqualTo(2));
            Assert.That(second.Last, Is.False);
            Assert.That(second.Window, Is.EqualTo(0.45f).Within(1e-6f));
            Assert.That(second.Clean.From, Is.EqualTo(0.12f).Within(1e-6f));
            Assert.That(second.Clean.To, Is.EqualTo(0.30f).Within(1e-6f));
            Assert.That(third.Last, Is.True);
            Assert.That(third.Window, Is.EqualTo(0.80f).Within(1e-6f));
            Assert.That(third.Clean.From, Is.EqualTo(0.42f).Within(1e-6f));
            Assert.That(third.Clean.To, Is.EqualTo(0.66f).Within(1e-6f));
            Assert.That(RecastRhythm.Press(kit, tap, 1, 0f).Any, Is.False, "the first press starts the chain: no window");
            Assert.That(RecastRhythm.Press(kit, tap, 4, 0f).Any, Is.False, "no fourth press");
            Assert.That(CleanPart.Of(kit, tap.RecastWindow, false, 0f, 3, 3).From, Is.EqualTo(0.42f).Within(1e-6f), "CleanPart.Of with the press's number is the same function");
        }

        [Test]
        public void FourthTapAndSteadyHand_EveryPressBeforeTheLastIsQuick_TheLastWaitsTheBeat()
        {
            var kit = new KitValues();
            SkillDefinition four = Skill("Triple Tap");
            four.RecastCount = 4; // Fourth Tap
            SkillDefinition five = FocusModeRules.InWindow(Skill("Triple Tap"), new FocusModeValues());
            Assert.That(five.RecastCount, Is.EqualTo(5), "Steady Hand's window: five");
            foreach (SkillDefinition s in new[] { four, five })
                for (int press = 2; press <= s.RecastCount; press++)
                {
                    RecastBeat b = RecastRhythm.Press(kit, s, press, 0f);
                    bool last = press == s.RecastCount;
                    Assert.That(b.Last, Is.EqualTo(last));
                    Assert.That(b.Window, Is.EqualTo(last ? 0.80f : 0.45f).Within(1e-6f), s.RecastCount + " presses, press " + press);
                    Assert.That(b.Clean.From, Is.EqualTo(last ? 0.42f : 0.12f).Within(1e-6f));
                }
        }

        [TestCase(0.05f, RecastPress.Shot, false, TestName = "Second_At005_AShotEarly_NotInRhythm")]
        [TestCase(0.20f, RecastPress.Shot, true, TestName = "Second_At020_AShotInRhythm")]
        [TestCase(0.40f, RecastPress.Shot, false, TestName = "Second_At040_AShotLate")]
        [TestCase(0.50f, RecastPress.None, false, TestName = "Second_At050_TheWindowHasRunOut_NoShot")]
        public void TheSecondPress_JudgedByItsOwnWindow(float since, RecastPress want, bool inRhythm)
        {
            var kit = new KitValues();
            SkillDefinition tap = Skill("Triple Tap");
            var chain = new RecastChain();
            chain.Start(Slot, 3, tap.RecastWindow);
            chain.NextWindow(RecastRhythm.Press(kit, tap, 2, 0f).Window);
            chain.ShotWentOff(10f);
            CleanPart part = RecastRhythm.Press(kit, tap, 2, 0f).Clean;
            Assert.That(chain.Judge(Slot, 10f + since, part, true), Is.EqualTo(want), "the second press is never a clean tap; it is a shot, or no shot at all");
            Assert.That(chain.InCleanPart(Slot, 10f + since, part), Is.EqualTo(inRhythm), "in rhythm: toward a full clean chain");
        }

        [TestCase(0.05f, RecastPress.Shot, TestName = "Third_At005_AShotEarly")]
        [TestCase(0.20f, RecastPress.Shot, TestName = "Third_At020_AShotEarly_TheOldCleanTimeIsNotCleanAnyMore")]
        [TestCase(0.40f, RecastPress.Shot, TestName = "Third_At040_AShotAHairEarly")]
        [TestCase(0.50f, RecastPress.Clean, TestName = "Third_At050_Clean")]
        [TestCase(0.70f, RecastPress.Shot, TestName = "Third_At070_AShotLate")]
        [TestCase(0.85f, RecastPress.None, TestName = "Third_At085_TheWindowHasRunOut_NoShot")]
        public void TheThirdPress_JudgedByItsOwnWindow(float since, RecastPress want)
        {
            var kit = new KitValues();
            SkillDefinition tap = Skill("Triple Tap");
            var chain = new RecastChain();
            chain.Start(Slot, 3, tap.RecastWindow);
            chain.NextWindow(RecastRhythm.Press(kit, tap, 2, 0f).Window);
            chain.ShotWentOff(10f);
            chain.Recast(true, RecastTiming.Clean);
            chain.NextWindow(RecastRhythm.Press(kit, tap, 3, 0f).Window);
            Assert.That(chain.Window, Is.EqualTo(0.80f).Within(1e-6f), "the last press's window, set while it is closed");
            chain.ShotWentOff(10.5f);
            CleanPart part = RecastRhythm.Press(kit, tap, 3, 0f).Clean;
            Assert.That(chain.Judge(Slot, 10.5f + since, part, true), Is.EqualTo(want));
        }

        [Test]
        public void TheOneFunction_AtItsEdges_BothInside_AndNeverPastItsWindow()
        {
            var kit = new KitValues();
            SkillDefinition tap = Skill("Triple Tap");
            CleanPart quick = RecastRhythm.Press(kit, tap, 2, 0f).Clean, last = RecastRhythm.Press(kit, tap, 3, 0f).Clean;
            Assert.That(quick.Contains(0.12f), Is.True, "its start inside");
            Assert.That(quick.Contains(0.30f), Is.True, "its end inside");
            Assert.That(quick.Contains(0.1198f), Is.False);
            Assert.That(quick.IsAfter(0.3002f), Is.True, "late after it");
            Assert.That(last.Contains(0.42f) && last.Contains(0.66f), Is.True);
            Assert.That(last.Contains(0.4198f) || last.Contains(0.6602f), Is.False);
            kit.QuickPressWindow = 0.2f; // a window shorter than its clean part: cut at the window's end
            RecastBeat cut = RecastRhythm.Press(kit, tap, 2, 0f);
            Assert.That(cut.Clean.To, Is.EqualTo(0.2f).Within(1e-6f));
            kit.QuickPressWindow = -1f; // below 0: the skill's own window
            kit.QuickPressCleanFrom = -1f; kit.QuickPressCleanTo = -1f; // below 0: the kit's CleanFrom and CleanTo
            RecastBeat own = RecastRhythm.Press(kit, tap, 2, 0f);
            Assert.That(own.Window, Is.EqualTo(tap.RecastWindow).Within(1e-6f));
            Assert.That(own.Clean.From, Is.EqualTo(kit.CleanFrom).Within(1e-6f));
            Assert.That(own.Clean.To, Is.EqualTo(kit.CleanTo).Within(1e-6f));
        }

        // ---------------- 2. Wide window, for each press ----------------

        [TestCase(2, 0.12f, 0.30f, 0.45f)]
        [TestCase(3, 0.42f, 0.66f, 0.80f)]
        public void WideWindow_WidensEachPress_RoundItsMiddle_InsideItsOwnWindow(int press, float from, float to, float window)
        {
            var kit = new KitValues();
            SkillDefinition tap = Skill("Triple Tap");
            RecastBeat wide = RecastRhythm.Press(kit, tap, press, 0.5f);
            float half = (to - from) * 0.25f;
            Assert.That(wide.Window, Is.EqualTo(window).Within(1e-6f), "the window is not widened");
            Assert.That(wide.Clean.From, Is.EqualTo(from - half).Within(1e-5f), "+50 %: a quarter of its width more at either edge");
            Assert.That(wide.Clean.To, Is.EqualTo(to + half).Within(1e-5f));
            RecastBeat whole = RecastRhythm.Press(kit, tap, press, 10f);
            Assert.That(whole.Clean.From, Is.EqualTo(0f).Within(1e-6f), "wider than the window: the whole window, and no more");
            Assert.That(whole.Clean.To, Is.EqualTo(window).Within(1e-6f));
            Assert.That(WideWindow.CleanPartOf(tap, kit, 0.5f).From, Is.EqualTo(RecastRhythm.Press(kit, tap, 3, 0.5f).Clean.From), "the card reads the last press through the same function");
        }

        // ---------------- 3. The line of a chain ----------------

        [Test]
        public void TheLine_OfASkill_ListsEveryPress_FromTheShotBeforeIt()
        {
            var kit = new KitValues();
            SkillDefinition tap = Skill("Triple Tap");
            var into = new RecastBeat[8];
            Assert.That(RecastRhythm.Line(tap, kit, 0f, into), Is.EqualTo(2), "presses 2 and 3");
            Assert.That(into[0].Press, Is.EqualTo(2));
            Assert.That(into[0].Clean.From, Is.EqualTo(0.12f).Within(1e-6f));
            Assert.That(into[1].Press, Is.EqualTo(3));
            Assert.That(into[1].Clean.From, Is.EqualTo(0.42f).Within(1e-6f));
            RecastLine line = RecastLine.Of(tap, kit, 0f);
            Assert.That(line.Any, Is.True);
            Assert.That(line.Count, Is.EqualTo(3));
            Assert.That(line.Next, Is.EqualTo(0), "no chain running");
            Assert.That(line.Beat(2).Window, Is.EqualTo(0.45f).Within(1e-6f));
            Assert.That(line.Beat(3).Window, Is.EqualTo(0.80f).Within(1e-6f));
            Assert.That(line.Timing(2), Is.EqualTo(RecastTiming.None));
            Assert.That(RecastLine.Of(Skill("Quick Shot"), kit, 0f).Any, Is.False, "no chain");
            var none = new RecastBeat[1];
            Assert.That(RecastRhythm.Line(tap, kit, 0f, none), Is.EqualTo(1), "as many as fit");
        }

        [Test]
        public void TheLine_OfAChainInProgress_SaysWhichPressIsNext_WhenTheShotBeforeWentOff_AndHowEachFell()
        {
            var kit = new KitValues();
            SkillDefinition tap = Skill("Triple Tap");
            var chain = new RecastChain();
            chain.Start(Slot, 3, tap.RecastWindow);
            chain.NextWindow(RecastRhythm.Press(kit, tap, 2, 0f).Window);
            RecastLine during = RecastLine.Of(chain, tap, kit, 0f, false, 10f);
            Assert.That(during.Next, Is.EqualTo(2));
            Assert.That(during.SinceShotBefore, Is.EqualTo(-1f), "the first shot has not gone off");
            chain.ShotWentOff(10f);
            RecastLine open = RecastLine.Of(chain, tap, kit, 0f, false, 10.2f);
            Assert.That(open.SinceShotBefore, Is.EqualTo(0.2f).Within(1e-5f));
            float since = 0.2f;
            CleanPart part = open.Beat(2).Clean;
            RecastTiming t2 = RecastRhythm.TimingOf(since, part, chain.InCleanPart(Slot, 10.2f, part), false, true);
            Assert.That(t2, Is.EqualTo(RecastTiming.Clean), "in rhythm");
            chain.Recast(true, t2);
            chain.NextWindow(RecastRhythm.Press(kit, tap, 3, 0f).Window);
            chain.ShotWentOff(10.5f);
            RecastLine third = RecastLine.Of(chain, tap, kit, 0f, false, 10.6f);
            Assert.That(third.Next, Is.EqualTo(3));
            Assert.That(third.Timing(2), Is.EqualTo(RecastTiming.Clean));
            Assert.That(third.Timing(3), Is.EqualTo(RecastTiming.None), "not made yet");
            Assert.That(RecastRhythm.TimingOf(0.1f, third.Beat(3).Clean, false, false, true), Is.EqualTo(RecastTiming.Early));
            Assert.That(RecastRhythm.TimingOf(0.7f, third.Beat(3).Clean, false, false, true), Is.EqualTo(RecastTiming.Late));
            Assert.That(RecastRhythm.TimingOf(0f, third.Beat(3).Clean, false, true, true), Is.EqualTo(RecastTiming.Waited), "a press that waited");
            Assert.That(RecastRhythm.TimingOf(0.5f, third.Beat(3).Clean, false, false, false), Is.EqualTo(RecastTiming.Plain), "a skill with no clean press");
            chain.MarkMissed();
            Assert.That(chain.TimingOf(3), Is.EqualTo(RecastTiming.Missed));
            chain.End();
            Assert.That(chain.TimingOf(2), Is.EqualTo(RecastTiming.Clean), "kept after the chain ended");
            chain.Start(Slot, 3, tap.RecastWindow);
            Assert.That(chain.TimingOf(2), Is.EqualTo(RecastTiming.None), "until the next starts");
        }

        [Test]
        public void TheLine_PrimedLastPress_IsCleanInItsWholeWindow_TheQuickPressesAsThey_Are()
        {
            var kit = new KitValues();
            SkillDefinition tap = Skill("Triple Tap");
            var chain = new RecastChain();
            chain.Start(Slot, 3, tap.RecastWindow);
            RecastLine primed = RecastLine.Of(chain, tap, kit, 0f, true, 10f);
            Assert.That(primed.Beat(2).Clean.From, Is.EqualTo(0.12f).Within(1e-6f));
            Assert.That(primed.Beat(3).Clean.From, Is.EqualTo(0f));
            Assert.That(primed.Beat(3).Clean.To, Is.EqualTo(0.80f).Within(1e-6f));
        }

        [Test]
        public void ANextWindow_WhileOneIsOpen_ChangesNothing()
        {
            var chain = new RecastChain();
            chain.Start(Slot, 3, 0.6f);
            chain.ShotWentOff(10f);
            chain.NextWindow(0.1f);
            Assert.That(chain.Window, Is.EqualTo(0.6f));
            Assert.That(chain.IsWindowOpen(10.3f), Is.True);
        }

        // ---------------- 4. As they were ----------------

        [Test]
        public void Backstep_ItsOnePress_AsItWas()
        {
            var kit = new KitValues();
            SkillDefinition step = Skill("Backstep");
            RecastBeat b = RecastRhythm.Press(kit, step, 2, 0f);
            Assert.That(b.Last, Is.True);
            Assert.That(b.Window, Is.EqualTo(0.6f).Within(1e-6f), "the skill's window");
            Assert.That(b.Clean.From, Is.EqualTo(0.03f).Within(1e-6f), "its own clean part after the hop (work order 064)");
            Assert.That(b.Clean.To, Is.EqualTo(0.23f).Within(1e-6f));
            CleanPart before = CleanPart.Of(kit, step.RecastWindow, true, 0.3f);
            RecastBeat wide = RecastRhythm.Press(kit, step, 2, 0.3f);
            Assert.That(wide.Clean.From, Is.EqualTo(before.From).Within(1e-6f), "widened as before");
            Assert.That(wide.Clean.To, Is.EqualTo(before.To).Within(1e-6f));
            step.RecastWindow += 0.2f; // Long Step
            Assert.That(RecastRhythm.Press(kit, step, 2, 0f).Window, Is.EqualTo(0.8f).Within(1e-6f), "Long Step's window as before");
        }

        [Test]
        public void TheSwitchOff_GivesTheMorningsChain_EveryWindowTheSkills_EveryCleanPartTheKits()
        {
            var kit = new KitValues { ChainRhythm = false };
            SkillDefinition tap = Skill("Triple Tap");
            for (int press = 2; press <= 3; press++)
            {
                RecastBeat b = RecastRhythm.Press(kit, tap, press, 0f);
                Assert.That(b.Window, Is.EqualTo(0.6f).Within(1e-6f));
                Assert.That(b.Clean.From, Is.EqualTo(0.10f).Within(1e-6f));
                Assert.That(b.Clean.To, Is.EqualTo(0.30f).Within(1e-6f));
            }
            kit.CleanEarly = false;
            Assert.That(RecastRhythm.Press(kit, tap, 3, 0f).Clean.From, Is.EqualTo(0.40f).Within(1e-5f), "and with CleanEarly off the last share, as before work order 040");
        }

        [Test]
        public void TheChargesAndTheCooldownBuffer_AsTheyWere()
        {
            var kit = new KitValues();
            Assert.That(kit.CooldownBuffer, Is.EqualTo(0.25f), "work order 066's buffer");
            SkillDefinition longShot = Skill("Long Shot");
            Assert.That(RecastRhythm.Press(kit, longShot, 2, 0f).Any, Is.False, "a skill with no chain has no press 2");
            Assert.That(WideWindow.HasBand(longShot), Is.True, "Long Shot's perfect band is its own, untouched here");
            SkillDefinition quick = Skill("Quick Shot");
            Assert.That(RecastLine.Of(quick, kit, 0f).Any, Is.False);
        }
    }
}
