using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Whether the developer's keys are on (work order 066, 2026-11-21, Kristian: "Bare utviklertastene av"): always in the
    /// Editor; in a built player only with "-dev" (or the benchmark's "-benchmark") on its command line.
    /// </summary>
    public class DeveloperKeysRuleTests
    {
        const string Exe = @"C:\Games\A Roguelike Game\A Roguelike Game.exe";

        [Test]
        public void InTheEditor_TheyAreOn_WithOrWithoutTheArgument()
        {
            Assert.That(DeveloperKeysRule.On(true, new string[0]), Is.True);
            Assert.That(DeveloperKeysRule.On(true, null), Is.True);
            Assert.That(DeveloperKeysRule.On(true, new[] { Exe }), Is.True);
        }

        [Test]
        public void InAPlayer_TheyAreOff_WithoutTheArgument()
        {
            Assert.That(DeveloperKeysRule.On(false, new[] { Exe }), Is.False, "started from its icon");
            Assert.That(DeveloperKeysRule.On(false, new string[0]), Is.False);
            Assert.That(DeveloperKeysRule.On(false, null), Is.False);
            Assert.That(DeveloperKeysRule.On(false, new[] { Exe, "-screen-fullscreen", "0", "-logFile", "log.txt" }), Is.False, "other arguments");
        }

        [Test]
        public void InAPlayer_DashDev_TurnsThemOn_InAnyCaseAndPlace()
        {
            Assert.That(DeveloperKeysRule.Argument, Is.EqualTo("-dev"));
            Assert.That(DeveloperKeysRule.On(false, new[] { Exe, "-dev" }), Is.True);
            Assert.That(DeveloperKeysRule.On(false, new[] { Exe, "-DEV" }), Is.True);
            Assert.That(DeveloperKeysRule.On(false, new[] { Exe, "-logFile", "log.txt", "-dev", "-screen-fullscreen", "0" }), Is.True);
        }

        [Test]
        public void InAPlayer_OnlyTheWholeArgumentCounts()
        {
            Assert.That(DeveloperKeysRule.On(false, new[] { Exe, "-developer" }), Is.False);
            Assert.That(DeveloperKeysRule.On(false, new[] { Exe, "dev" }), Is.False);
            Assert.That(DeveloperKeysRule.On(false, new[] { Exe, "--dev" }), Is.False);
            Assert.That(DeveloperKeysRule.On(false, new[] { Exe, "-benchmarkLabel", "x" }), Is.False, "a benchmark option without the benchmark");
            Assert.That(DeveloperKeysRule.On(false, new[] { Exe, null, "x" }), Is.False);
        }

        [Test]
        public void InAPlayer_TheBenchmark_RunsWithThemOn_AsItDidBefore()
        {
            Assert.That(DeveloperKeysRule.BenchmarkArgument, Is.EqualTo("-benchmark"));
            Assert.That(DeveloperKeysRule.On(false, new[] { Exe, "-benchmark", "-benchmarkLabel", "x" }), Is.True);
        }
    }
}
