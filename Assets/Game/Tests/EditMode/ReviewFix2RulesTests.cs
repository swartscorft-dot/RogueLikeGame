using System;
using System.IO;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 033 (the second reviews of 2026-11-20), as rules: a run's picks counted in one place; a focus window
    /// ended without losing the gauge's focus; and the between-runs save, which never loses its last good file: a
    /// replace that fails keeps the old one and the new one beside it, loading reads the newest readable of the two, and
    /// a file that is there but cannot be read now is not an empty save.
    /// </summary>
    public class ReviewFix2RulesTests
    {
        string _folder;
        string SavePath => Path.Combine(_folder, "between-runs.json");

        [SetUp]
        public void MakeFolder() => _folder = Path.Combine(Path.GetTempPath(), "ReviewFix2RulesTests-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void RemoveFolder()
        {
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
        }

        // ---------------- 6. One count of a run's picks ----------------

        [Test]
        public void ARunsPicks_CountEveryKind_TheSkillUpgradesToo()
        {
            var r = new RunRecord("ABCDEFGH", 5);
            Assert.That(r.Picks, Is.Zero);
            r.TakeSkill("Fan Fire");
            r.TakeUpgrade("+10 hp");
            r.TakeSkillUpgrade("Quick Shot: Double Tap");
            Assert.That(r.Picks, Is.EqualTo(3), "a skill upgrade from an elite room is a pick");
            r.TakeFocusChoice("Steady Hand");
            Assert.That(r.Picks, Is.EqualTo(4), "and the focus choice");
            r.End(RunOutcome.Died, 10f, 0, 100);
            r.TakeSkill("Long Shot");
            Assert.That(r.Picks, Is.EqualTo(4), "nothing after the end");
        }

        // ---------------- 12. A window ended, the focus kept ----------------

        [Test]
        public void AFocusWindow_Ended_IsOver_AndTheGaugeKeepsItsFocus_AndFillsAgain()
        {
            var values = new IdentityValues();
            var gauge = new FocusGauge();
            gauge.Fill(values);
            Assert.That(gauge.TryActivate(10f, values), Is.True);
            Assert.That(gauge.Add(5f, values, 11f), Is.EqualTo(0f), "refused while the window runs");
            gauge.EndWindow();
            Assert.That(gauge.IsEmpowered(11f), Is.False);
            Assert.That(gauge.EmpoweredRemaining(11f), Is.EqualTo(0f));
            Assert.That(gauge.Add(5f, values, 11f), Is.EqualTo(5f), "it fills again at once");
            gauge.EndWindow();
            Assert.That(gauge.Focus, Is.EqualTo(5f), "ending no window keeps the focus");
        }

        // ---------------- 10. A failed replace never loses the last good save ----------------

        static MetaState WithCurrency(int c) => new MetaState { Currency = c };

        [Test]
        public void AReplaceThatFails_KeepsTheLastGoodSave_AndTheNewOneBesideIt_IsWhatIsRead()
        {
            Assert.That(MetaSaveFile.Save(SavePath, WithCurrency(10), out string error), Is.True, error);
            string old = File.ReadAllText(SavePath);
            bool saved;
            // Held open by another program: it cannot be replaced, nor deleted, now.
            using (new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                saved = MetaSaveFile.Save(SavePath, WithCurrency(99), out error);
            Assume.That(saved, Is.False, "the replace failed on this machine");
            Assert.That(error, Is.Not.Empty, "the failure is said");
            Assert.That(File.ReadAllText(SavePath), Is.EqualTo(old), "the last good save is untouched");
            MetaState read = MetaSaveFile.Load(SavePath, new MetaValues(), out MetaLoad how, out _);
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That(read.Currency, Is.EqualTo(99), "the new one, kept beside it, is read");

            // The next save that can replace it does, and the one beside is not needed any more.
            Assert.That(MetaSaveFile.Save(SavePath, WithCurrency(120), out error), Is.True, error);
            Assert.That(File.Exists(MetaSaveFile.BesidePath(SavePath)), Is.False);
            Assert.That(MetaSaveFile.Load(SavePath, new MetaValues(), out how, out _).Currency, Is.EqualTo(120));
        }

        [Test]
        public void TwoSaves_TheNewestReadableIsRead_ADamagedNewerOneFallsBackToTheOther()
        {
            Assert.That(MetaSaveFile.Save(SavePath, WithCurrency(10), out string error), Is.True, error);
            string beside = MetaSaveFile.BesidePath(SavePath);
            File.WriteAllText(beside, MetaJson.Write(WithCurrency(20)));
            File.SetLastWriteTimeUtc(SavePath, DateTime.UtcNow.AddMinutes(-2));
            File.SetLastWriteTimeUtc(beside, DateTime.UtcNow.AddMinutes(-1));
            Assert.That(MetaSaveFile.Load(SavePath, new MetaValues(), out MetaLoad how, out _).Currency, Is.EqualTo(20), "the newer one");
            Assert.That(how, Is.EqualTo(MetaLoad.Read));

            File.SetLastWriteTimeUtc(beside, DateTime.UtcNow.AddMinutes(-3));
            Assert.That(MetaSaveFile.Load(SavePath, new MetaValues(), out how, out _).Currency, Is.EqualTo(10), "the main one when it is the newer");

            File.WriteAllText(beside, "{ \"version\": 1, \"curr");
            File.SetLastWriteTimeUtc(beside, DateTime.UtcNow);
            MetaState s = MetaSaveFile.Load(SavePath, new MetaValues(), out how, out string kept);
            Assert.That(s.Currency, Is.EqualTo(10), "the newer one damaged: the other");
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That(kept, Does.StartWith(beside + ".damaged-"), "the damaged one kept aside, as ever");
            Assert.That(File.Exists(SavePath), Is.True);
        }

        // ---------------- 15. A save that cannot be read now is not an empty one ----------------

        [Test]
        public void ASaveThatCannotBeReadNow_IsUnreadable_NotEmpty_AndIsLeftAlone()
        {
            Assert.That(MetaSaveFile.Save(SavePath, WithCurrency(50), out string error), Is.True, error);
            string text = File.ReadAllText(SavePath);
            MetaLoad how;
            using (new FileStream(SavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                MetaSaveFile.Load(SavePath, new MetaValues(), out how, out _);
            Assert.That(how, Is.EqualTo(MetaLoad.Unreadable));
            Assert.That(File.ReadAllText(SavePath), Is.EqualTo(text), "left as it was");
            Assert.That(MetaSaveFile.Load(SavePath, new MetaValues(), out how, out _).Currency, Is.EqualTo(50), "read once it can be");
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
        }
    }
}
