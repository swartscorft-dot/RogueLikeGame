using System.Collections.Generic;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 125 (Kristian, 2026-11-25, of work order 119's pictures: "man ser mange fragmenterte pixler rundt omkring, det
    /// ser veldig dårlig ut"): the clean line's and the anti-aliasing's switches on by default, and the fragmentation measure seeing
    /// what it is meant to see: a whole ring passes, the same ring broken into specks and short pieces fails, a ring with a side
    /// missing fails, and a line that jumps from frame to frame is caught.
    /// </summary>
    public class ToonLookCleanRuleTests
    {
        [TearDown]
        public void PutBack()
        {
            ToonLookValues.CleanInk = true;
            AntiAliasingValues.On = true;
        }

        [Test]
        public void TheCleanLineAndTheAntiAliasing_AreOnByDefault_AndHerLineStaysWorkOrder119s()
        {
            Assert.That(ToonLookValues.CleanInk, Is.True);
            Assert.That(AntiAliasingValues.On, Is.True);
            Assert.That(AntiAliasingValues.MsaaNow || AntiAliasingValues.SmaaNow, Is.True);
            var v = new ToonLookValues();
            Assert.That(v.InkWidth, Is.EqualTo(1.25f), "the thinner line he asked for (work order 119)");
            Assert.That(v.InkDepthPush, Is.EqualTo(0.06f));
            AntiAliasingValues.On = false;
            Assert.That(AntiAliasingValues.MsaaNow || AntiAliasingValues.SmaaNow, Is.False, "off: none, as before");
        }

        const int W = 120, H = 120;

        /// <summary>A disc of radius <paramref name="r"/> in the middle: the figure.</summary>
        static bool[] Disc(float r)
        {
            var f = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = x - W / 2f, dy = y - H / 2f;
                    f[y * W + x] = dx * dx + dy * dy <= r * r;
                }
            return f;
        }

        /// <summary>The ring just outside a disc of radius <paramref name="r"/>, <paramref name="width"/> px wide, but where <paramref name="keep"/> says no.</summary>
        static bool[] Ring(float r, float width, System.Func<int, int, bool> keep = null)
        {
            var f = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = x - W / 2f, dy = y - H / 2f, d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                    f[y * W + x] = d > r && d <= r + width && (keep == null || keep(x, y));
                }
            return f;
        }

        [Test]
        public void AWholeRing_Passes()
        {
            InkFragments.Result r = InkFragments.Measure(Ring(40f, 1.5f), W, H, Disc(40f));
            Assert.That(r.Pieces, Is.EqualTo(1), r.ToString());
            Assert.That(r.Passes, Is.True, r.ToString());
            Assert.That(r.OutlineInked, Is.GreaterThan(0.99f), r.ToString());
            Assert.That(r.OutlineWhole, Is.True, r.ToString());
        }

        [Test]
        public void TheSameRing_BrokenIntoSpecksAndShortPieces_Fails()
        {
            // Every pixel kept by a fixed scatter, as the hull's line breaks on thin bones: thick in one place, gone in the next.
            bool[] broken = Ring(40f, 1.5f, (x, y) => ((x * 7 + y * 13) % 5) < 2);
            InkFragments.Result r = InkFragments.Measure(broken, W, H, Disc(40f));
            Assert.That(r.Passes, Is.False, r.ToString());
            Assert.That(r.Specks, Is.GreaterThan(10), r.ToString());
        }

        [Test]
        public void ARingWithASideMissing_IsCaught_ByTheOutline()
        {
            bool[] half = Ring(40f, 1.5f, (x, y) => x < W / 2 + 10);
            InkFragments.Result r = InkFragments.Measure(half, W, H, Disc(40f));
            Assert.That(r.ShareInLarge, Is.GreaterThan(0.95f), "what is there is whole");
            Assert.That(r.OutlineInked, Is.LessThan(InkFragments.PassOutlineInked), r.ToString());
            Assert.That(r.OutlineWhole, Is.False, r.ToString());
        }

        [Test]
        public void TheInkPixels_AreThoseTheLineMadeClearlyDarker()
        {
            float[] without = { 200f, 200f, 30f, 120f };
            float[] with = { 15f, 190f, 22f, 60f };
            bool[] ink = InkFragments.InkOf(with, without);
            Assert.That(ink, Is.EqualTo(new[] { true, false, false, true }), "a dark pixel made a little darker is not ink; a mid one halved is");
        }

        [Test]
        public void ALineThatJumpsFromFrameToFrame_IsCaught()
        {
            Assert.That(InkFragments.FrameJump(new List<int> { 500, 510, 495, 505 }), Is.LessThan(InkFragments.PassFrameJump));
            Assert.That(InkFragments.FrameJump(new List<int> { 500, 300, 520, 480 }), Is.GreaterThan(InkFragments.PassFrameJump));
        }
    }
}
