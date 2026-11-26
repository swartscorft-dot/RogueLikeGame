using System;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 142's rule (<see cref="DrawnEdges"/>): a drawn edge is the judged edge, to the frame, line widths included. For
    /// Triple Tap's second and third presses and Backstep's second, at the kit's numbers: where the ring's light line first touches
    /// the band's outer light line and where any of its ink first touches any of the band's (entering), where it has fully left the
    /// inner light line and all the band's ink (leaving), and where the cylinder's chamber meets the notch's posts, each against the
    /// judged edge in px (degrees) and ms. With the rule on every number is within one frame; with it off (work order 134's drawing)
    /// the ring's are not. The table is written to the test's output. A later drawing of the cue gets its rows here.
    /// </summary>
    public class DrawnEdgesTests
    {
        [TearDown]
        public void PutBack() => DrawnEdges.On = true;

        struct Press
        {
            public string Name;
            public RecastBeat Beat;
            public float Notch;
        }

        static Press[] Presses()
        {
            var kit = new KitValues();
            var v = new TimingAtTheSightValues();
            RecastBeat tt2 = RecastRhythm.Press(kit, 0.6f, false, 2, 3, 0f), tt3 = RecastRhythm.Press(kit, 0.6f, false, 3, 3, 0f);
            RecastBeat bs2 = RecastRhythm.Press(kit, 0.6f, true, 2, 2, 0f);
            // The notch, fixed for a chain, as TimingAtTheSight.StartChain makes it (Triple Tap's startup 0.15 s; Backstep's shot 0.08 s).
            float ttNotch = CylinderCue.Notch(CylinderCue.Spacing(CylinderCue.Chambers(3, false), v),
                                              Math.Max(CylinderCue.Reach(tt2.Clean, 0.15f), CylinderCue.Reach(tt3.Clean, 0.15f)), v);
            float bsNotch = CylinderCue.Notch(CylinderCue.Spacing(CylinderCue.Chambers(2, true), v), CylinderCue.Reach(bs2.Clean, 0.2f), v);
            return new[]
            {
                new Press { Name = "Triple Tap press 2", Beat = tt2, Notch = ttNotch },
                new Press { Name = "Triple Tap press 3", Beat = tt3, Notch = ttNotch },
                new Press { Name = "Backstep press 2", Beat = bs2, Notch = bsNotch },
            };
        }

        static string F(float x) => x.ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>The table's rows for the rule on or off: px (degrees for the notch) and ms before the judged entry, after the judged end.</summary>
        static float[][] Table(bool honest, StringBuilder log)
        {
            var v = new TimingAtTheSightValues();
            DrawnEdges.RingBand b = DrawnEdges.Band(v, honest);
            Press[] presses = Presses();
            var rows = new float[presses.Length][];
            log.AppendLine("Work order 142, the drawn edges " + (honest ? "WITH the rule" : "as work order 134 drew them")
                           + " (px of a 1080 screen; ms before the judged start for entering, after the judged end for leaving; one frame is 16.7 ms):");
            for (int i = 0; i < presses.Length; i++)
            {
                CleanPart c = presses[i].Beat.Clean;
                float speed = RingCue.Speed(c, v); // px a second
                float lightIn = b.LightEnters - v.BandOuter, inkIn = b.InkEnters - v.BandOuter;
                float lightOut = v.BandInner - b.LightLeaves, inkOut = v.BandInner - b.InkLeaves;
                float notch = presses[i].Notch, turn = CylinderCue.Speed(c, notch); // degrees a second
                float postR = v.CylinderRadius + v.ChamberRadius + v.PostGap + v.PostLength * 0.5f;
                float post = DrawnEdges.NotchPost(notch, v.ChamberRadius, v.CylinderRadius, v.PostWidth, postR, honest);
                float meets = DrawnEdges.NotchMeets(post, v.ChamberRadius, v.CylinderRadius, v.PostWidth, postR) - notch * 0.5f; // degrees before the judged edge
                rows[i] = new[] { lightIn / speed, inkIn / speed, lightOut / speed, inkOut / speed, meets / turn };
                log.AppendLine("  " + presses[i].Name + " (clean " + F(c.From * 1000f) + " to " + F(c.To * 1000f) + " ms; ring " + F(speed) + " px/s, cylinder " + F(turn) + " deg/s):"
                               + " ring light lines touch " + F(lightIn) + " px / " + F(rows[i][0] * 1000f) + " ms early;"
                               + " any ink touches " + F(inkIn) + " px / " + F(rows[i][1] * 1000f) + " ms early;"
                               + " light line leaves " + F(lightOut) + " px / " + F(rows[i][2] * 1000f) + " ms late;"
                               + " all ink leaves " + F(inkOut) + " px / " + F(rows[i][3] * 1000f) + " ms late;"
                               + " chamber meets the post " + F(meets) + " deg / " + F(rows[i][4] * 1000f) + " ms early");
            }
            return rows;
        }

        [Test]
        public void WithTheRule_EveryDrawnEdgeOfTheRingAndTheCylinder_IsWithinOneFrameOfTheJudgedEdge()
        {
            var log = new StringBuilder();
            float[][] before = Table(false, log);
            float[][] after = Table(true, log);
            TestContext.Out.WriteLine(log.ToString());
            for (int i = 0; i < after.Length; i++)
                for (int k = 0; k < after[i].Length; k++)
                    Assert.That(Math.Abs(after[i][k]), Is.LessThanOrEqualTo(DrawnEdges.OneFrame + 1e-5f), "row " + i + ", column " + k + ": " + after[i][k] * 1000f + " ms");
            // The light lines meet exactly at the judged radius.
            for (int i = 0; i < after.Length; i++) Assert.That(after[i][0], Is.EqualTo(0f).Within(1e-5f), "the light lines touch at the judged edge");
            // Work order 134's drawing broke it: its light lines touched 2.5 px early, more than a frame at the third press.
            Assert.That(before[1][0], Is.GreaterThan(DrawnEdges.OneFrame), "before the rule the ring's light lines touched more than a frame early at Triple Tap's third press");
            Assert.That(before[1][1], Is.GreaterThan(4f * DrawnEdges.OneFrame), "and their outlines more than four frames early");
        }

        [Test]
        public void TheSwitchOff_TheBandAsWorkOrder134DrewIt()
        {
            var v = new TimingAtTheSightValues();
            DrawnEdges.RingBand b = DrawnEdges.Band(v, false);
            Assert.That(b.Outer, Is.EqualTo(v.BandOuter));
            Assert.That(b.Inner, Is.EqualTo(v.BandInner));
            Assert.That(b.OuterDark, Is.EqualTo(v.BandOuter));
            Assert.That(b.DarkWidth, Is.EqualTo(DrawnEdges.BandLine + DrawnEdges.DarkMore));
            Assert.That(b.RingDarkWidth, Is.EqualTo(v.RingStroke + DrawnEdges.DarkMore));
            Assert.That(DrawnEdges.BladeLean(0.5f, false), Is.EqualTo(0.35f));
            Assert.That(DrawnEdges.BladeLean(0.5f, true), Is.EqualTo(0.5f), "on: the blade leans as the zones");
        }

        [Test]
        public void WithTheRule_TheRingLooksInsideTheLines_ExactlyWhileItsRadiusIsInTheJudgedBand()
        {
            var v = new TimingAtTheSightValues();
            DrawnEdges.RingBand b = DrawnEdges.Band(v, true);
            for (float r = 10f; r <= 60f; r += 0.05f)
            {
                bool judged = r <= v.BandOuter + 1e-4f && r >= v.BandInner - 1e-4f;
                Assert.That(b.LooksInside(r), Is.EqualTo(judged), "radius " + r);
            }
        }
    }
}
