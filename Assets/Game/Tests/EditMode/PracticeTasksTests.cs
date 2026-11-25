using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 133 (2026-11-25, Kristian ticked "Øvingsrom med oppgaver"): the practice room's tasks as rules. The list made from her
    /// starting kit (in key order, each skill by its own name and key, a clean press after the skills that have one, marks only with a
    /// builder and a spender, the duck as the cover key works, the last plate as the start menu is there); each task's done-condition
    /// (what counts, and what does not: a miss, the wrong skill, a press before the chain's last, a press that was not clean, too few
    /// marks, a duck away from the room's prop); the run of them (a tick, the beat, events in the beat, skip, end, the last plate);
    /// the saved flag; the switch off; the keys made readable; the words plain.
    /// </summary>
    public class PracticeTasksTests
    {
        static readonly string[] KeysInOrder = { "Q", "1", "2", "3", "R", "4", "F", "RMB" };

        /// <summary>The start skills of the settings on their keys, in key order, as the runtime makes them (slot = place in the skill list).</summary>
        static List<PracticeSkill> StartKit()
        {
            SkillDefinition[] all = SkillDefaults.Create();
            // The keys of decision 0005: the four start skills on the keys they had (slot i on key i for the first eight).
            var kit = new List<PracticeSkill>();
            for (int key = 0; key < KeysInOrder.Length; key++)
                if (all[key].StartEquipped) kit.Add(PracticeSkill.Of(key, all[key], KeysInOrder[key]));
            return kit;
        }

        static PracticeKeys Keys(PracticeDuck duck = PracticeDuck.Hold, bool menu = true, string focus = "Z") => new PracticeKeys
        {
            Up = "W", Left = "A", Down = "S", Right = "D", Shoot = "LMB", Dash = "Space", DashToCursor = true, Cover = "Left Ctrl", Duck = duck,
            Focus = focus, Start = menu ? "Enter" : "F5", StartMenu = menu,
        };

        static List<PracticeTask> StartList(PracticeDuck duck = PracticeDuck.Hold, bool menu = true) =>
            PracticeTaskList.Make(new PracticeTasksValues(), Keys(duck, menu), StartKit());

        static PracticeEvent E(PracticeEventKind kind, int slot = -1, bool dummy = true, int shot = 0, bool clean = false, int marks = 0, float metres = 0f, bool prop = false)
        {
            PracticeEvent e = PracticeEvent.Of(kind);
            e.Slot = slot;
            e.OnDummy = dummy;
            e.Shot = shot;
            e.Clean = clean;
            e.Marks = marks;
            e.Metres = metres;
            e.AtPracticeProp = prop;
            return e;
        }

        const int QuickShot = 0, ScatterBlast = 4, Backstep = 6, TripleTap = 7;

        // ---------------- The values and the switch ----------------

        [Test]
        public void TheValues_AreTheOrdersFirstGuesses_AndTheSwitchIsOn()
        {
            var v = new PracticeTasksValues();
            Assert.That(v.On, Is.True, "his tick of 2026-11-25: on by default");
            Assert.That(v.MoveMetres, Is.EqualTo(4f));
            Assert.That(v.ShotHits, Is.EqualTo(3));
            Assert.That(v.Dashes, Is.EqualTo(1));
            Assert.That(v.MarksToSpend, Is.EqualTo(2));
            Assert.That(v.TickBeat, Is.InRange(0.5f, 2f), "a short beat");
            Assert.That(v.FirstDelay, Is.EqualTo(1f));
            Assert.That(v.InTheFirstRun, Is.True, "his answer of 2026-11-25: the first run teaches");
            Assert.That(v.TargetModel, Is.EqualTo(PropModel.Statue), "the project's own crypt statue");
            Assert.That(v.Targets, Is.EqualTo(3));
            Assert.That(v.NearTheLight, Is.LessThan(v.FarFromTheLight), "stood by the braziers");
        }

        [Test]
        public void TheSwitchOff_TheTasksNeverBeginByThemselves_InTheGreyRoomOrInTheFirstRun()
        {
            var v = new PracticeTasksValues { On = false, InTheFirstRun = false };
            Assert.That(PracticeTaskFlag.BeginsByItself(v, false, false, true, 100f), Is.False);
            Assert.That(PracticeTaskFlag.BeginsInTheRun(v, false, true, 1, true, false), Is.False);
            v.InTheFirstRun = true;
            Assert.That(PracticeTaskFlag.BeginsInTheRun(v, false, true, 1, true, false), Is.False, "the first run's own bool is under the switch");
            v.On = true;
            v.InTheFirstRun = false;
            Assert.That(PracticeTaskFlag.BeginsByItself(v, false, false, true, 100f), Is.True, "the same moment with the switch on: the grey room's version alone");
            Assert.That(PracticeTaskFlag.BeginsInTheRun(v, false, true, 1, true, false), Is.False);
        }

        // ---------------- The first run's teaching room (the follow-up) ----------------

        [Test]
        public void TheFirstRun_Teaches_OnlyOnFloorOne_InItsEntrance_BeforeTheFlag_OnceAFloor()
        {
            var v = new PracticeTasksValues();
            Assert.That(PracticeTaskFlag.BeginsInTheRun(v, false, true, 1, true, false), Is.True);
            Assert.That(PracticeTaskFlag.BeginsInTheRun(v, true, true, 1, true, false), Is.False, "the flag set: later runs start as today");
            Assert.That(PracticeTaskFlag.BeginsInTheRun(v, false, false, 1, true, false), Is.False, "not in a run");
            Assert.That(PracticeTaskFlag.BeginsInTheRun(v, false, true, 2, true, false), Is.False, "floor 2");
            Assert.That(PracticeTaskFlag.BeginsInTheRun(v, false, true, 1, false, false), Is.False, "not in the entrance room");
            Assert.That(PracticeTaskFlag.BeginsInTheRun(v, false, true, 1, true, true), Is.False, "this floor taught already");
            Assert.That(PracticeTaskFlag.BeginsByItself(v, false, false, true, 100f), Is.False, "with the first run teaching, the grey room waits for H");
        }

        [Test]
        public void InTheRun_TheLastPlateSaysTheDoorLeadsOn_HasNoKey_AndIsDoneByWalkingOut()
        {
            PracticeKeys k = Keys();
            k.InRun = true;
            List<PracticeTask> list = PracticeTaskList.Make(null, k, StartKit());
            Assert.That(list.Count, Is.EqualTo(StartList().Count), "the same thirteen");
            PracticeTask last = list.Last();
            Assert.That(last.Kind, Is.EqualTo(PracticeTaskKind.GoOn));
            Assert.That(last.Words, Is.EqualTo("That is the kit. The door is open: the run goes on."));
            Assert.That(last.Keys, Is.Empty);
            Assert.That(PracticeTaskRules.Counts(last, E(PracticeEventKind.LeftTheRoom)), Is.EqualTo(1f));
            Assert.That(PracticeTaskRules.Counts(last, E(PracticeEventKind.LeftForARun)), Is.Zero);
            Assert.That(list.Take(list.Count - 1).Select(t => t.Words), Is.EqualTo(StartList().Take(list.Count - 1).Select(t => PracticeWords.InTheRun(t.Words))), "the other plates as in the grey room, its targets statues");
            Assert.That(list.Any(t => t.Words.Contains("dummy")), Is.False, "no dummy in the crypt");
            Assert.That(list[1].Words, Is.EqualTo("Aim with the mouse and click to shoot a statue."));
        }

        [Test]
        public void TheDoorOpens_WhenTheLastPlateIsUp_OrTheyAreOver_AndTheClockIsHeldUntilSheWalksOut()
        {
            PracticeKeys k = Keys();
            k.InRun = true;
            var run = new PracticeTaskRun(PracticeTaskList.Make(null, k, StartKit()));
            Assert.That(PracticeTaskFlag.DoorOpens(run), Is.False, "the first task: shut");
            for (int i = 0; i < 30 && !run.OnLast; i++) run.Skip();
            Assert.That(PracticeTaskFlag.DoorOpens(run), Is.True, "the last plate leads on");
            var ended = new PracticeTaskRun(PracticeTaskList.Make(null, k, StartKit()));
            ended.End();
            Assert.That(PracticeTaskFlag.DoorOpens(ended), Is.True, "ended with H");
            Assert.That(PracticeTaskFlag.HoldsTheClock(true, false, true), Is.True, "the door shut");
            Assert.That(PracticeTaskFlag.HoldsTheClock(true, true, true), Is.True, "open, she still in the room");
            Assert.That(PracticeTaskFlag.HoldsTheClock(true, true, false), Is.False, "out: the run's clock runs");
            Assert.That(PracticeTaskFlag.HoldsTheClock(false, false, true), Is.False, "no teaching");
        }

        [Test]
        public void InTheRun_TheCleanPressTasksSkillIsReadyWhenTheTaskBegins_InTheGreyRoomNot()
        {
            PracticeTask clean = StartList().First(t => t.Kind == PracticeTaskKind.CleanPress);
            Assert.That(PracticeTaskFlag.ReadyAtStart(clean, true), Is.True);
            Assert.That(PracticeTaskFlag.ReadyAtStart(clean, false), Is.False);
            Assert.That(PracticeTaskFlag.ReadyAtStart(StartList().First(t => t.Kind == PracticeTaskKind.UseSkill), true), Is.False, "only the clean presses (the producer's decision)");
        }

        [Test]
        public void TheRings_ShowOnlyWhileATaskWaitsForAHitOnATarget_AndTheirSwitchIsOn()
        {
            Assert.That(new PracticeTasksValues().TargetsWearARing, Is.True, "the producer's addition, on");
            var needs = StartList().Where(PracticeTaskRules.NeedsATarget).Select(t => t.Kind + (t.Slot >= 0 ? ":" + t.Slot : "")).ToList();
            Assert.That(needs, Is.EqualTo(new[] { "Shoot", "UseSkill:" + QuickShot, "UseSkill:" + ScatterBlast, "SpendMarks:" + ScatterBlast }),
                        "the basic shots, the skills used by a hit, the marks; not a chain's presses, its clean press, the move, the dash, the duck, focus, the last plate");
            Assert.That(PracticeTaskRules.NeedsATarget(null), Is.False);
        }

        [Test]
        public void TheTargetsPlaces_StartStraightAhead_AtTheDistance_TurningEitherWay()
        {
            List<GroundPoint> c = PracticeTaskPlaces.Candidates(new GroundPoint(10f, 10f), new GroundPoint(0f, 5f), 6f);
            Assert.That(c[0].X, Is.EqualTo(10f).Within(1e-3f));
            Assert.That(c[0].Z, Is.EqualTo(16f).Within(1e-3f), "straight ahead, 6 m");
            Assert.That((c[1] - new GroundPoint(10f, 10f)).Length, Is.EqualTo(6f).Within(1e-3f));
            Assert.That(c[1].X, Is.Not.EqualTo(10f).Within(0.1f), "then turned");
            Assert.That(c.Count, Is.GreaterThan(30), "three rings of turns");
            Assert.That(PracticeTaskPlaces.Candidates(new GroundPoint(0f, 0f), new GroundPoint(0f, 0f), 6f)[0].Z, Is.EqualTo(6f).Within(1e-3f), "no way ahead: north");
        }

        // ---------------- The list ----------------

        [Test]
        public void TheList_FromHerStartingKit_IsTheProducersCorrected()
        {
            List<PracticeTask> list = StartList();
            Assert.That(list.Select(t => t.Kind + (t.Slot >= 0 ? ":" + t.Slot : "")), Is.EqualTo(new[]
            {
                "Move", "Shoot", "Dash",
                "UseSkill:" + QuickShot, "UseSkill:" + ScatterBlast, "UseSkill:" + Backstep, "CleanPress:" + Backstep,
                "UseSkill:" + TripleTap, "CleanPress:" + TripleTap,
                "SpendMarks:" + ScatterBlast, "Duck", "Focus", "StartRun",
            }), "move, shoot, dash, the four in key order (Q, R, F, RMB) with the two clean presses, marks, the duck, focus, the last plate");
        }

        [Test]
        public void EachSkillTask_NamesItsSkill_AndShowsItsOwnKey()
        {
            List<PracticeTask> list = StartList();
            var names = new Dictionary<int, string> { { QuickShot, "Quick Shot" }, { ScatterBlast, "Scatter Blast" }, { Backstep, "Backstep" }, { TripleTap, "Triple Tap" } };
            var keys = new Dictionary<int, string> { { QuickShot, "Q" }, { ScatterBlast, "R" }, { Backstep, "F" }, { TripleTap, "RMB" } };
            foreach (PracticeTask t in list.Where(t => t.Kind == PracticeTaskKind.UseSkill || t.Kind == PracticeTaskKind.CleanPress))
            {
                StringAssert.Contains(names[t.Slot], t.Words, "its own name");
                Assert.That(t.Keys, Is.EqualTo(new[] { keys[t.Slot] }), "its own key, as a key");
            }
            Assert.That(list[0].Keys, Is.EqualTo(new[] { "W", "A", "S", "D" }));
            Assert.That(list[1].Keys, Is.EqualTo(new[] { "LMB" }));
            Assert.That(list[2].Keys, Is.EqualTo(new[] { "Space" }));
            Assert.That(list.First(t => t.Kind == PracticeTaskKind.Duck).Keys, Is.EqualTo(new[] { "Left Ctrl" }));
            Assert.That(list.First(t => t.Kind == PracticeTaskKind.Focus).Keys, Is.EqualTo(new[] { "Z" }));
            Assert.That(list.Last().Keys, Is.EqualTo(new[] { "Enter" }));
        }

        [Test]
        public void TheCleanPressTasks_SayWhatToWatch_TheBoxAndTheWhiteBar()
        {
            foreach (PracticeTask t in StartList().Where(t => t.Kind == PracticeTaskKind.CleanPress))
            {
                StringAssert.Contains("box lights up", t.Words);
                StringAssert.Contains("white", t.Words);
            }
            Assert.That(StartList().Count(t => t.Kind == PracticeTaskKind.CleanPress), Is.EqualTo(2), "Backstep and Triple Tap have one; Quick Shot and Scatter Blast have none");
        }

        [Test]
        public void TheMarksTask_NamesTheBuildersKeys_AndTheSpender()
        {
            PracticeTask marks = StartList().First(t => t.Kind == PracticeTaskKind.SpendMarks);
            Assert.That(marks.Words, Is.EqualTo("Mark the dummy with Q, F or RMB, then spend the marks up close with Scatter Blast."));
            Assert.That(marks.Need, Is.EqualTo(2f));
            Assert.That(marks.Keys, Is.EqualTo(new[] { "R" }));
        }

        [Test]
        public void AKitWithoutASpender_HasNoMarksTask_AndOneWithoutABuilderNeither()
        {
            List<PracticeSkill> noSpender = StartKit().Where(s => !s.SpendsMarks).ToList();
            Assert.That(PracticeTaskList.Make(null, Keys(), noSpender).Any(t => t.Kind == PracticeTaskKind.SpendMarks), Is.False);
            List<PracticeSkill> noBuilder = StartKit().Where(s => !s.BuildsMarks).ToList();
            Assert.That(PracticeTaskList.Make(null, Keys(), noBuilder).Any(t => t.Kind == PracticeTaskKind.SpendMarks), Is.False);
        }

        [Test]
        public void TheDuckTask_FollowsTheCoverKeysWay_AndIsLeftOutWhenTheKeyDoesNothing()
        {
            StringAssert.Contains("hold the key", StartList(PracticeDuck.Hold).First(t => t.Kind == PracticeTaskKind.Duck).Words);
            StringAssert.Contains("tap the key", StartList(PracticeDuck.Tap).First(t => t.Kind == PracticeTaskKind.Duck).Words);
            StringAssert.Contains("take cover", StartList(PracticeDuck.TakeCover).First(t => t.Kind == PracticeTaskKind.Duck).Words);
            StringAssert.Contains("orange edge", StartList(PracticeDuck.Hold).First(t => t.Kind == PracticeTaskKind.Duck).Words, "the friend did not understand the orange edge");
            Assert.That(StartList(PracticeDuck.None).Any(t => t.Kind == PracticeTaskKind.Duck), Is.False);
        }

        [Test]
        public void TheLastPlate_SaysHowToStartARun_WithTheMenuOrWithoutIt()
        {
            PracticeTask withMenu = StartList(menu: true).Last(), without = StartList(menu: false).Last();
            Assert.That(withMenu.Kind, Is.EqualTo(PracticeTaskKind.StartRun));
            StringAssert.Contains("START RUN", withMenu.Words);
            Assert.That(withMenu.Keys, Is.EqualTo(new[] { "Enter" }));
            Assert.That(without.Keys, Is.EqualTo(new[] { "F5" }));
            StringAssert.Contains("start a run", without.Words);
        }

        [Test]
        public void TheShootingTask_SaysClick_UnlessTheRuleKeepsFiring()
        {
            StringAssert.Contains("click", StartList()[1].Words);
            PracticeKeys held = Keys();
            held.ShootHeld = true;
            StringAssert.Contains("hold", PracticeTaskList.Make(null, held, StartKit())[1].Words);
        }

        [Test]
        public void TheDashTask_FollowsTheDashDirection()
        {
            StringAssert.Contains("toward the cursor", StartList()[2].Words);
            PracticeKeys k = Keys();
            k.DashToCursor = false;
            StringAssert.Contains("the way you move", PracticeTaskList.Make(null, k, StartKit())[2].Words);
        }

        [Test]
        public void EveryLine_IsShortPlainAndHasNoExclamationMark()
        {
            foreach (PracticeDuck duck in new[] { PracticeDuck.Hold, PracticeDuck.Tap, PracticeDuck.TakeCover })
                foreach (bool menu in new[] { true, false })
                    foreach (PracticeTask t in StartList(duck, menu))
                    {
                        Assert.That(t.Words, Does.Not.Contain("!"), t.Words);
                        Assert.That(t.Words.Length, Is.InRange(10, 90), "one short line: " + t.Words);
                        Assert.That(t.Words.TrimEnd(), Does.EndWith("."), t.Words);
                        Assert.That(t.Keys.Length, Is.InRange(1, 4));
                        Assert.That(t.Keys.All(k => !string.IsNullOrEmpty(k)), Is.True, t.Kind.ToString());
                    }
        }

        // ---------------- Each task's done-condition ----------------

        static PracticeTask Task(PracticeTaskKind kind, int slot = -1) => StartList().First(t => t.Kind == kind && (slot < 0 || t.Slot == slot));

        [Test]
        public void Move_CountsTheMetresWalked_AndNothingElse()
        {
            PracticeTask move = Task(PracticeTaskKind.Move);
            Assert.That(PracticeTaskRules.Counts(move, E(PracticeEventKind.Walked, metres: 0.25f)), Is.EqualTo(0.25f));
            Assert.That(PracticeTaskRules.Counts(move, E(PracticeEventKind.Dashed)), Is.Zero, "a dash is not walking");
            Assert.That(PracticeTaskRules.Counts(move, E(PracticeEventKind.ShotHit)), Is.Zero);
            Assert.That(move.Need, Is.EqualTo(4f));
        }

        [Test]
        public void Shoot_CountsBasicShotsThatHitADummy()
        {
            PracticeTask shoot = Task(PracticeTaskKind.Shoot);
            Assert.That(PracticeTaskRules.Counts(shoot, E(PracticeEventKind.ShotHit)), Is.EqualTo(1f));
            Assert.That(PracticeTaskRules.Counts(shoot, E(PracticeEventKind.ShotHit, dummy: false)), Is.Zero, "not on a dummy");
            Assert.That(PracticeTaskRules.Counts(shoot, E(PracticeEventKind.SkillHit, QuickShot)), Is.Zero, "a skill is not the basic shot");
            Assert.That(shoot.Need, Is.EqualTo(3f));
        }

        [Test]
        public void Dash_CountsADashThatStarted()
        {
            PracticeTask dash = Task(PracticeTaskKind.Dash);
            Assert.That(PracticeTaskRules.Counts(dash, E(PracticeEventKind.Dashed)), Is.EqualTo(1f));
            Assert.That(PracticeTaskRules.Counts(dash, E(PracticeEventKind.Walked, metres: 5f)), Is.Zero);
        }

        [Test]
        public void UsingASkill_IsAHitOnADummyBythatSkill_OrForAChainItsLastPress()
        {
            PracticeTask quick = Task(PracticeTaskKind.UseSkill, QuickShot);
            Assert.That(PracticeTaskRules.Counts(quick, E(PracticeEventKind.SkillHit, QuickShot)), Is.EqualTo(1f));
            Assert.That(PracticeTaskRules.Counts(quick, E(PracticeEventKind.SkillHit, QuickShot, dummy: false)), Is.Zero, "a miss on the dummy");
            Assert.That(PracticeTaskRules.Counts(quick, E(PracticeEventKind.SkillHit, ScatterBlast)), Is.Zero, "another skill");
            Assert.That(PracticeTaskRules.Counts(quick, E(PracticeEventKind.ShotHit)), Is.Zero, "the basic shot");

            PracticeTask tap = Task(PracticeTaskKind.UseSkill, TripleTap);
            Assert.That(tap.Presses, Is.EqualTo(3));
            Assert.That(PracticeTaskRules.Counts(tap, E(PracticeEventKind.SkillHit, TripleTap)), Is.Zero, "one tap's hit is not the chain");
            Assert.That(PracticeTaskRules.Counts(tap, E(PracticeEventKind.Recast, TripleTap, shot: 2)), Is.Zero, "the second press is not the last");
            Assert.That(PracticeTaskRules.Counts(tap, E(PracticeEventKind.Recast, TripleTap, shot: 3)), Is.EqualTo(1f), "the third press fired: used");

            PracticeTask step = Task(PracticeTaskKind.UseSkill, Backstep);
            Assert.That(step.Presses, Is.EqualTo(2));
            Assert.That(PracticeTaskRules.Counts(step, E(PracticeEventKind.Recast, Backstep, shot: 2)), Is.EqualTo(1f), "the hop, then the shot");
        }

        [Test]
        public void ACleanPress_IsOnlyAPressTheGameJudgedClean_OfThatSkill()
        {
            PracticeTask clean = Task(PracticeTaskKind.CleanPress, TripleTap);
            Assert.That(PracticeTaskRules.Counts(clean, E(PracticeEventKind.Recast, TripleTap, shot: 3, clean: false)), Is.Zero, "the last press, not clean");
            Assert.That(PracticeTaskRules.Counts(clean, E(PracticeEventKind.Recast, TripleTap, shot: 3, clean: true)), Is.EqualTo(1f));
            Assert.That(PracticeTaskRules.Counts(clean, E(PracticeEventKind.Recast, Backstep, shot: 2, clean: true)), Is.Zero, "another skill's clean press");
            Assert.That(PracticeTaskRules.Counts(clean, E(PracticeEventKind.SkillHit, TripleTap)), Is.Zero);
        }

        [Test]
        public void SpendingMarks_IsOneHitOfTheSpenderTakingEnoughMarksOffADummy()
        {
            PracticeTask marks = Task(PracticeTaskKind.SpendMarks);
            Assert.That(PracticeTaskRules.Counts(marks, E(PracticeEventKind.MarksSpent, ScatterBlast, marks: 1)), Is.Zero, "one mark is too few");
            Assert.That(PracticeTaskRules.Counts(marks, E(PracticeEventKind.MarksSpent, ScatterBlast, marks: 3)), Is.EqualTo(marks.Need));
            Assert.That(PracticeTaskRules.Counts(marks, E(PracticeEventKind.MarksSpent, Backstep, marks: 3)), Is.Zero, "the spender, not Backstep's clean spend");
            Assert.That(PracticeTaskRules.Counts(marks, E(PracticeEventKind.MarksSpent, ScatterBlast, dummy: false, marks: 3)), Is.Zero, "on a dummy");
            var run = new PracticeTaskRun(new List<PracticeTask> { marks, Task(PracticeTaskKind.StartRun) });
            run.Note(E(PracticeEventKind.MarksSpent, ScatterBlast, marks: 1), 0f);
            run.Note(E(PracticeEventKind.MarksSpent, ScatterBlast, marks: 1), 0.1f);
            Assert.That(run.Ticked, Is.False, "two spends of one mark do not add up");
        }

        [Test]
        public void TheDuck_IsDownOrInCoverAtThePracticeRoomsProp()
        {
            PracticeTask duck = Task(PracticeTaskKind.Duck);
            Assert.That(PracticeTaskRules.Counts(duck, E(PracticeEventKind.Ducked, prop: true)), Is.EqualTo(1f));
            Assert.That(PracticeTaskRules.Counts(duck, E(PracticeEventKind.TookCover, prop: true)), Is.EqualTo(1f));
            Assert.That(PracticeTaskRules.Counts(duck, E(PracticeEventKind.Ducked, prop: false)), Is.Zero, "beside something else");
        }

        [Test]
        public void Focus_IsFocusSpent_AndTheLastPlate_IsLeavingForARun()
        {
            Assert.That(PracticeTaskRules.Counts(Task(PracticeTaskKind.Focus), E(PracticeEventKind.FocusSpent)), Is.EqualTo(1f));
            Assert.That(PracticeTaskRules.Counts(Task(PracticeTaskKind.Focus), E(PracticeEventKind.MarksSpent, ScatterBlast, marks: 5)), Is.Zero);
            Assert.That(PracticeTaskRules.Counts(Task(PracticeTaskKind.StartRun), E(PracticeEventKind.LeftForARun)), Is.EqualTo(1f));
            Assert.That(PracticeTaskRules.Counts(Task(PracticeTaskKind.Move), E(PracticeEventKind.LeftForARun)), Is.Zero, "leaving is only the last plate's");
        }

        // ---------------- The run of them ----------------

        [Test]
        public void ADoneTask_Ticks_AndTheNextComesAfterTheBeat_EventsInTheBeatCountForNothing()
        {
            var run = new PracticeTaskRun(StartList());
            Assert.That(run.Current.Kind, Is.EqualTo(PracticeTaskKind.Move));
            Assert.That(run.Note(E(PracticeEventKind.Walked, metres: 3f), 0f), Is.False);
            Assert.That(run.Progress, Is.EqualTo(3f));
            Assert.That(run.Note(E(PracticeEventKind.Walked, metres: 1.5f), 1f), Is.True, "4 m walked");
            Assert.That(run.Ticked, Is.True);
            Assert.That(run.Note(E(PracticeEventKind.ShotHit), 1.2f), Is.False, "in the beat: nothing counts");
            run.Step(1.5f, 1.1f);
            Assert.That(run.Index, Is.Zero, "still in the beat");
            run.Step(2.11f, 1.1f);
            Assert.That(run.Index, Is.EqualTo(1));
            Assert.That(run.Current.Kind, Is.EqualTo(PracticeTaskKind.Shoot));
            Assert.That(run.Progress, Is.Zero, "the shot in the beat did not carry over");
        }

        [Test]
        public void TheListRunsToItsEnd_DoneOneByOne()
        {
            List<PracticeTask> list = StartList();
            var run = new PracticeTaskRun(list);
            float now = 0f;
            var events = new Dictionary<PracticeTaskKind, PracticeEvent>
            {
                { PracticeTaskKind.Move, E(PracticeEventKind.Walked, metres: 5f) },
                { PracticeTaskKind.Dash, E(PracticeEventKind.Dashed) },
                { PracticeTaskKind.Duck, E(PracticeEventKind.Ducked, prop: true) },
                { PracticeTaskKind.Focus, E(PracticeEventKind.FocusSpent) },
                { PracticeTaskKind.StartRun, E(PracticeEventKind.LeftForARun) },
            };
            for (int guard = 0; guard < 100 && !run.Over; guard++)
            {
                PracticeTask t = run.Current;
                PracticeEvent e;
                if (t.Kind == PracticeTaskKind.Shoot) e = E(PracticeEventKind.ShotHit);
                else if (t.Kind == PracticeTaskKind.UseSkill) e = t.Presses > 1 ? E(PracticeEventKind.Recast, t.Slot, shot: t.Presses) : E(PracticeEventKind.SkillHit, t.Slot);
                else if (t.Kind == PracticeTaskKind.CleanPress) e = E(PracticeEventKind.Recast, t.Slot, shot: t.Presses, clean: true);
                else if (t.Kind == PracticeTaskKind.SpendMarks) e = E(PracticeEventKind.MarksSpent, t.Slot, marks: 5);
                else e = events[t.Kind];
                run.Note(e, now);
                now += 2f;
                run.Step(now, 1.1f);
            }
            Assert.That(run.Finished, Is.True);
            Assert.That(run.Ended, Is.False);
            Assert.That(run.Done, Is.EqualTo(list.Count));
            Assert.That(run.Skipped, Is.Zero);
            Assert.That(PracticeTaskFlag.Saves(run), Is.True, "finished: the flag is saved");
        }

        [Test]
        public void Skip_GoesOnAtOnce_AndFromTheLastPlateFinishes()
        {
            var run = new PracticeTaskRun(StartList());
            run.Skip();
            Assert.That(run.Index, Is.EqualTo(1));
            Assert.That(run.Skipped, Is.EqualTo(1));
            for (int i = 0; i < 30 && !run.OnLast; i++) run.Skip();
            Assert.That(run.Current.Kind, Is.EqualTo(PracticeTaskKind.StartRun));
            Assert.That(run.Over, Is.False);
            run.Skip();
            Assert.That(run.Finished, Is.True);
            Assert.That(run.Done, Is.Zero);
        }

        [Test]
        public void End_StopsThemAtOnce_AndSavesTheFlag_NothingCountsAfter()
        {
            var run = new PracticeTaskRun(StartList());
            Assert.That(PracticeTaskFlag.Saves(run), Is.False, "running: nothing saved");
            run.End();
            Assert.That(run.Ended, Is.True);
            Assert.That(run.Current, Is.Null);
            Assert.That(run.Note(E(PracticeEventKind.Walked, metres: 9f), 0f), Is.False);
            Assert.That(PracticeTaskFlag.Saves(run), Is.True);
        }

        // ---------------- The saved flag ----------------

        [Test]
        public void TheTasksBeginByThemselves_OnlyTheFirstTime_InTheRoom_AfterTheFirstDelay()
        {
            var v = new PracticeTasksValues { InTheFirstRun = false }; // the grey room's version alone
            Assert.That(PracticeTaskFlag.BeginsByItself(v, false, false, true, 1f), Is.True);
            Assert.That(PracticeTaskFlag.BeginsByItself(v, true, false, true, 100f), Is.False, "finished or ended before: never by themselves again");
            Assert.That(PracticeTaskFlag.BeginsByItself(v, false, true, true, 100f), Is.False, "running already");
            Assert.That(PracticeTaskFlag.BeginsByItself(v, false, false, false, 100f), Is.False, "not in the practice room (the menu up, a run)");
            Assert.That(PracticeTaskFlag.BeginsByItself(v, false, false, true, 0.5f), Is.False, "the first delay");
            Assert.That(PracticeTaskFlag.Read(PracticeTaskFlag.Stored), Is.True);
            Assert.That(PracticeTaskFlag.Read(0), Is.False, "nothing stored");
        }

        // ---------------- The keys ----------------

        [Test]
        public void TheKeys_AreShownAsTheBindingsNameThem_MadeReadable()
        {
            Assert.That(PracticeWords.KeyName("LeftCtrl"), Is.EqualTo("Left Ctrl"));
            Assert.That(PracticeWords.KeyName("LeftShift"), Is.EqualTo("Left Shift"));
            Assert.That(PracticeWords.KeyName("Digit1"), Is.EqualTo("1"));
            Assert.That(PracticeWords.KeyName("Space"), Is.EqualTo("Space"));
            Assert.That(PracticeWords.KeyName("LMB"), Is.EqualTo("LMB"));
            Assert.That(PracticeWords.KeyName("RMB"), Is.EqualTo("RMB"));
            Assert.That(PracticeWords.KeyName("F5"), Is.EqualTo("F5"));
            Assert.That(PracticeWords.KeyName("Q"), Is.EqualTo("Q"));
            Assert.That(PracticeWords.KeyName(""), Is.EqualTo(""));
        }
    }
}
