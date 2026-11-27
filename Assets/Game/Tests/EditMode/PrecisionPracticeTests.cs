using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 143's practice task, "Stop and shoot": at the end of the teaching, before its last plate, only with precision in
    /// use; done by basic-shot hits on a target from 8 m or more, each fired accurate after a run; in the first run's teaching room
    /// its words name a statue.
    /// </summary>
    public class PrecisionPracticeTests
    {
        static PracticeKeys Keys(bool precision, bool inRun) => new PracticeKeys
        {
            Up = "W", Left = "A", Down = "S", Right = "D", Shoot = "LMB", Dash = "Space", Cover = "Ctrl", Duck = PracticeDuck.Hold, Focus = "Z",
            Start = "Enter", StartMenu = true, InRun = inRun, StopAndShoot = precision,
        };

        [Test]
        public void TheTask_ComesLastBeforeTheLastPlate_OnlyWithPrecisionInUse()
        {
            List<PracticeTask> without = PracticeTaskList.Make(new PracticeTasksValues(), Keys(false, false), null);
            Assert.That(without.Any(t => t.Kind == PracticeTaskKind.StopAndShoot), Is.False, "precision Off: the list as before");
            List<PracticeTask> with = PracticeTaskList.Make(new PracticeTasksValues(), Keys(true, false), null);
            Assert.That(with.Count, Is.EqualTo(without.Count + 1));
            Assert.That(with[with.Count - 2].Kind, Is.EqualTo(PracticeTaskKind.StopAndShoot), "before the last plate");
            Assert.That(with[with.Count - 1].Kind, Is.EqualTo(PracticeTaskKind.StartRun));
            Assert.That(with[with.Count - 2].Need, Is.EqualTo(3f), "three hits");
            Assert.That(PracticeTaskRules.NeedsATarget(with[with.Count - 2]), Is.True, "the statues' rings show for it");
            List<PracticeTask> inRun = PracticeTaskList.Make(new PracticeTasksValues(), Keys(true, true), null);
            PracticeTask task = inRun.Single(t => t.Kind == PracticeTaskKind.StopAndShoot);
            StringAssert.Contains("a statue", task.Words);
            Assert.That(inRun[inRun.Count - 1].Kind, Is.EqualTo(PracticeTaskKind.GoOn));
        }

        [Test]
        public void TheTask_CountsHitsFromAfar_FiredAccurateAfterARun()
        {
            var task = new PracticeTask { Kind = PracticeTaskKind.StopAndShoot, Need = 3f };
            PracticeEvent Hit(float metres, bool stopped, bool dummy = true, int slot = -1)
            {
                PracticeEvent e = PracticeEvent.Of(slot < 0 ? PracticeEventKind.ShotHit : PracticeEventKind.SkillHit);
                e.Slot = slot;
                e.OnDummy = dummy;
                e.Metres = metres;
                e.StoppedShot = stopped;
                return e;
            }
            Assert.That(PracticeTaskRules.Counts(task, Hit(9f, true)), Is.EqualTo(1f));
            Assert.That(PracticeTaskRules.Counts(task, Hit(8f, true)), Is.EqualTo(1f), "8 m exactly");
            Assert.That(PracticeTaskRules.Counts(task, Hit(7.5f, true)), Is.EqualTo(0f), "too near");
            Assert.That(PracticeTaskRules.Counts(task, Hit(9f, false)), Is.EqualTo(0f), "not fired accurate after a run");
            Assert.That(PracticeTaskRules.Counts(task, Hit(9f, true, dummy: false)), Is.EqualTo(0f), "not a target of the room");
            Assert.That(PracticeTaskRules.Counts(task, Hit(9f, true, slot: 0)), Is.EqualTo(0f), "a skill's hit");
        }
    }
}
