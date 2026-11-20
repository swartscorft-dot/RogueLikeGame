using System.Collections.Generic;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>The benchmark's sums of a frame's numbers (work order 032, 2026-11-20): <see cref="FrameStats"/>.</summary>
    public class FrameStatsTests
    {
        static List<double> OneToHundred()
        {
            var v = new List<double>();
            for (int i = 100; i >= 1; i--) v.Add(i); // not sorted
            return v;
        }

        [Test]
        public void Percentile_IsTheNearestRank()
        {
            List<double> v = OneToHundred();
            Assert.That(FrameStats.Percentile(v, 95), Is.EqualTo(95));
            Assert.That(FrameStats.Percentile(v, 99), Is.EqualTo(99));
            Assert.That(FrameStats.Percentile(v, 100), Is.EqualTo(100));
            Assert.That(FrameStats.Percentile(v, 0), Is.EqualTo(1));
            Assert.That(FrameStats.Percentile(new List<double> { 3, 1, 2 }, 50), Is.EqualTo(2));
            Assert.That(FrameStats.Percentile(new List<double> { 5 }, 99), Is.EqualTo(5));
        }

        [Test]
        public void MeanMaxAndMedian()
        {
            List<double> v = OneToHundred();
            Assert.That(FrameStats.Mean(v), Is.EqualTo(50.5).Within(1e-9));
            Assert.That(FrameStats.Max(v), Is.EqualTo(100));
            Assert.That(FrameStats.Median(new List<double> { 9, 1, 5 }), Is.EqualTo(5));
            Assert.That(FrameStats.Median(new List<double> { 4, 1, 3, 2 }), Is.EqualTo(2.5));
        }

        [Test]
        public void NothingGivesZero()
        {
            var none = new List<double>();
            Assert.That(FrameStats.Mean(none), Is.EqualTo(0));
            Assert.That(FrameStats.Max(none), Is.EqualTo(0));
            Assert.That(FrameStats.Percentile(none, 95), Is.EqualTo(0));
            Assert.That(FrameStats.Median(none), Is.EqualTo(0));
        }
    }
}
