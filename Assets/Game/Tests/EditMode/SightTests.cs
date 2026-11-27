using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 144, the sight's rules: part A's numbers; the arm's teeth zone by work order 142's rule (<see cref="DrawnEdges"/>),
    /// in the manner of <see cref="DrawnEdgesTests"/>: for Triple Tap's second and last presses and Backstep's shot, at the kit's
    /// numbers, the clock time at which the catch first touches the zone and the last at which any of it is still in it, against
    /// the judged start and end of the clean part, in degrees and ms (the table is written to the test's output; part A's zone,
    /// drawn to the catch's leading edge at both ends, beside it); the opening; the fourth cue and the Trials page's line; and the
    /// switch off, the game as before (the four cues, no line).
    /// </summary>
    public class SightTests
    {
        static readonly SightValues V = new SightValues();

        [TearDown]
        public void PutBack()
        {
            SightValues.On = true;
            SightValues.PointerStays = true;
        }

        struct Press
        {
            public string Name;
            public RecastBeat Beat;
            public float Lead;
        }

        static Press[] Presses()
        {
            var kit = new KitValues();
            // The leads: the skills' startups, PlayerCharacter.RecastLead (Triple Tap 0.15 s, Backstep's hop 0.2 s), as DrawnEdgesTests and part A use.
            return new[]
            {
                new Press { Name = "Triple Tap press 2", Beat = RecastRhythm.Press(kit, 0.6f, false, 2, 3, 0f), Lead = 0.15f },
                new Press { Name = "Triple Tap press 3", Beat = RecastRhythm.Press(kit, 0.6f, false, 3, 3, 0f), Lead = 0.15f },
                new Press { Name = "Backstep press 2", Beat = RecastRhythm.Press(kit, 0.6f, true, 2, 2, 0f), Lead = 0.2f },
            };
        }

        static string F(float x) => x.ToString("0.0", CultureInfo.InvariantCulture);

        const float Step = 1e-4f; // s: the scan's step, a tenth of a millisecond

        /// <summary>The first and last clock times (seconds since the window opened) at which the catch, as drawn, is in the zone.</summary>
        static void Scan(Press p, float start, float end, out float first, out float last)
        {
            first = float.NaN;
            last = float.NaN;
            int steps = (int)((p.Lead + p.Beat.Window + 0.2f) / Step);
            for (int i = 0; i <= steps; i++)
            {
                float t = -p.Lead + i * Step;
                float lead = SightArm.LeadAngle(SightArm.HeadAngle(SightArm.SincePress(t, p.Lead), V), V);
                if (!SightArm.CatchInZone(lead, start, end, V)) continue;
                if (float.IsNaN(first)) first = t;
                last = t;
            }
        }

        [Test]
        public void TheZone_TheCatchEntersAndLeaves_WithinOneFrameOfTheJudgedStartAndEnd_ForEachTimedPress()
        {
            var log = new StringBuilder("Work order 144, the sight's teeth zone (degrees clockwise from the top; ms against the judged edge; one frame is 16.7 ms; the head climbs "
                                        + F(V.Rate) + " degrees a second, its catch " + F(SightArm.CatchSpan(V)) + " degrees wide):\n");
            foreach (Press p in Presses())
            {
                CleanPart c = p.Beat.Clean;
                Assert.That(SightArm.Zone(c, p.Lead, V, out float start, out float end), Is.True, p.Name + ": a zone");
                Scan(p, start, end, out float first, out float last);
                SightArm.ZoneOfPartA(c, p.Lead, V, out float aStart, out float aEnd);
                Scan(p, aStart, aEnd, out float aFirst, out float aLast);
                float pxPerDegree = (float)(2.0 * Math.PI * V.TeethInner / 360.0);
                log.AppendLine("  " + p.Name + " (clean " + F(c.From * 1000f) + " to " + F(c.To * 1000f) + " ms after the window opens, lead " + F(p.Lead * 1000f) + " ms): zone "
                               + F(start) + " to " + F(end) + " (" + F(start - end) + " degrees, " + F((start - end) * pxPerDegree) + " px at the teeth's tips); the catch enters at "
                               + F(first * 1000f) + " ms (" + F((first - c.From) * 1000f) + " ms against the judge), last in at " + F(last * 1000f) + " ms (" + F((last - c.To) * 1000f)
                               + "); part A's zone " + F(aStart) + " to " + F(aEnd) + " (" + F(aStart - aEnd) + " degrees): enters " + F((aFirst - c.From) * 1000f)
                               + ", last in " + F((aLast - c.To) * 1000f) + " ms against the judge");
                Assert.That(Math.Abs(first - c.From), Is.LessThanOrEqualTo(DrawnEdges.OneFrame), p.Name + ": the catch first touches the zone on the judge's frame");
                Assert.That(Math.Abs(last - c.To), Is.LessThanOrEqualTo(DrawnEdges.OneFrame), p.Name + ": and has wholly left it on the first late frame");
                Assert.That(Math.Abs(first - c.From), Is.LessThanOrEqualTo(2f * Step), p.Name + ": in fact to the scan's step");
                Assert.That(Math.Abs(last - c.To), Is.LessThanOrEqualTo(2f * Step), p.Name);
                Assert.That(start - end, Is.EqualTo((c.To - c.From) * V.Rate - SightArm.CatchSpan(V)).Within(1e-3f), p.Name + ": the zone is its seconds at one rate, less the catch's width");
                Assert.That(aLast - c.To, Is.GreaterThan(DrawnEdges.OneFrame), p.Name + ": part A's zone let the catch's tip stay in it more than a frame after the end");
            }
            TestContext.Out.WriteLine(log.ToString());
        }

        [Test]
        public void TheSightSaysClean_ExactlyWhenTheJudgeDoes_AtEveryTenthOfAMillisecondOfEachWindow()
        {
            foreach (Press p in Presses())
            {
                CleanPart c = p.Beat.Clean;
                int disagree = 0, late = 0;
                int steps = (int)(p.Beat.Window / Step);
                for (int i = 0; i <= steps; i++)
                {
                    float t = i * Step;
                    bool judge = c.Contains(t);
                    if (SightArm.SaysClean(t, p.Beat.Window, c, p.Lead, V) != judge && Math.Abs(t - c.From) > Step && Math.Abs(t - c.To) > Step) disagree++;
                    if (SightArm.IsLate(t, p.Beat.Window, c) != (t > c.To + 1e-5f)) late++;
                }
                Assert.That(disagree, Is.EqualTo(0), p.Name + ": moments the sight and the judge disagree (away from the edges' rounding)");
                Assert.That(late, Is.EqualTo(0), p.Name + ": too late exactly after the clean part");
                Assert.That(SightArm.SaysClean(-0.05f, p.Beat.Window, c, p.Lead, V), Is.False, "never before the window opens");
                Assert.That(SightArm.SaysClean(p.Beat.Window + 0.05f, p.Beat.Window, c, p.Lead, V), Is.False, "nor after it");
            }
        }

        [Test]
        public void TheArm_LeavesThePinAtThePress_OneRateForEveryPress_AndTheZonesAreItsSeconds()
        {
            Assert.That(SightArm.RestAngle(V), Is.EqualTo(136f), "folded 14 degrees from the pin at 150");
            Assert.That(SightArm.HeadAngle(0f, V), Is.EqualTo(136f), "at the press");
            Assert.That(SightArm.HeadAngle(-1f, V), Is.EqualTo(136f), "never below its pin");
            Assert.That(SightArm.HeadAngle(0.5f, V), Is.EqualTo(86f).Within(1e-4f), "100 degrees a second");
            Assert.That(SightArm.HeadAngle(100f, V), Is.EqualTo(136f - V.MostSweep).Within(1e-3f), "a long window never wraps it onto its pin");
            Assert.That(SightArm.LeadAngle(86f, V), Is.EqualTo(80f), "its catch 6 degrees ahead of its middle");
            Press[] presses = Presses();
            var widths = new List<float>();
            foreach (Press p in presses)
            {
                SightArm.Zone(p.Beat.Clean, p.Lead, V, out float start, out float end);
                widths.Add(start - end);
                int teeth = SightArm.Teeth(start, end, V, out float tooth);
                Assert.That(teeth, Is.GreaterThanOrEqualTo(2), p.Name + ": the first and last teeth mark the ends");
                Assert.That(SightArm.ToothStart(0, teeth, start, end, tooth), Is.EqualTo(start), p.Name + ": the first tooth flush with the zone's start");
                Assert.That(SightArm.ToothStart(teeth - 1, teeth, start, end, tooth) - tooth, Is.EqualTo(end).Within(1e-4f), p.Name + ": the last flush with its end");
                Assert.That(end, Is.GreaterThan(-90f).And.LessThan(start), p.Name + ": the zone in the sight's upper right, above the pin");
            }
            TestContext.Out.WriteLine("zones: Triple Tap press 2 " + F(widths[0]) + ", press 3 " + F(widths[1]) + ", Backstep " + F(widths[2]) + " degrees");
            Assert.That(widths[1], Is.GreaterThan(widths[0]), "Triple Tap's last press wider than its second");
        }

        [Test]
        public void TheOpening_TheBracketsInkAtTheConesEdge_SnugUntilTheConeIsWiderThanTheirClosedPlace()
        {
            Assert.That(SightOpening.ClosedInk(V), Is.EqualTo(24.5f), "the band's 26 px less its 1.5 px outline");
            Assert.That(SightOpening.Shift(0f, V), Is.EqualTo(0f), "precision Off, or accurate: snug");
            Assert.That(SightOpening.Shift(20f, V), Is.EqualTo(0f), "a cone narrower than the ring: closed");
            Assert.That(SightOpening.Shift(24.5f, V), Is.EqualTo(0f));
            Assert.That(SightOpening.Shift(126f, V), Is.EqualTo(101.5f).Within(1e-4f), "part A's 24 degrees at 10 m (252 px across): the brackets' inner ink on the cone's edge");
        }

        [Test]
        public void TheNumbers_ArePartAs_TheSwitchesOn()
        {
            Assert.That(SightValues.On, Is.True, "on with his words");
            Assert.That(SightValues.PointerStays, Is.True, "variant P, the producer's pick");
            Assert.That(V.RingInner, Is.EqualTo(26f));
            Assert.That(V.RingOuter, Is.EqualTo(32f));
            Assert.That(V.LineRadius, Is.EqualTo(27.25f));
            Assert.That(V.DotRadius + V.Outline, Is.EqualTo(4f), "the dot 8 px across with its outline: the floor round it open to 24.5 px");
            Assert.That(V.PinAngle, Is.EqualTo(150f));
            Assert.That(V.Rate, Is.EqualTo(100f));
            Assert.That(V.BracketLineAlpha * 255f, Is.EqualTo(120f).Within(1e-3f), "round 2: 120 of 255 (80 was too faint on the dark floors)");
            Assert.That(V.Gunmetal.R * 255f, Is.EqualTo(96f).Within(1e-3f), "round 2: the gunmetal a step lighter than part A's 64, 70, 78");
            Assert.That(V.Gunmetal.G * 255f, Is.EqualTo(105f).Within(1e-3f));
            Assert.That(V.Gunmetal.B * 255f, Is.EqualTo(117f).Within(1e-3f));
            Assert.That(V.BracketLineAlpha, Is.LessThan(V.Teal.A), "the brackets' line stays under the pointer's own full teal");
            Assert.That(V.RackOuter * 2f, Is.EqualTo(98f), "about 98 px across while a press is timed");
            Assert.That(V.Late.R, Is.Not.EqualTo(new InterfaceValues().Red.R), "the dull red is not the enemies' warning red");
        }

        [Test]
        public void TheFourthCue_AndTheTrialsPagesLine_AndTheSwitchOff_TheFourCuesAndNoLine()
        {
            Assert.That(TimingCues.Offered, Is.EqualTo(5));
            Assert.That(TimingCues.Step(4), Is.EqualTo(TimingCue.Sight), "a stored sight comes back");
            Assert.That(TimingCues.Default, Is.EqualTo(TimingCue.BarOnly), "the default cue stays what it is");
            Assert.That(TimingCueLines.CueWords(TimingCue.Sight), Is.EqualTo("Sight"));
            Assert.That(TimingCues.SoundByDefault(TimingCue.Sight), Is.True, "the click with it, as with every cue but the bar");
            var lines = new List<SettingLine>();
            SettingsPages.Lines(SettingsPage.Trials, true, lines);
            // The merge of 2026-11-27, W2 (changed deliberately): right after work order 143's line (134's click with 143 off); work
            // orders 135's and 136's lines come after it, so it is no longer the last.
            SettingLine before = PrecisionValues.On ? SettingLine.Precision : SettingLine.TimingClick;
            Assert.That(lines.IndexOf(SettingLine.SightOpening), Is.EqualTo(lines.IndexOf(before) + 1), "work order 144's line after " + before);
            var withoutSight = new List<SettingLine>(lines); // the page as it was before work order 144, for the switch off below
            withoutSight.Remove(SettingLine.SightOpening);
            Assert.That(SettingsPages.PageOf(SettingLine.SightOpening), Is.EqualTo(SettingsPage.Trials));
            Assert.That(SightLines.PointerStays(-1), Is.True, "none stored: the switch's own");
            Assert.That(SightLines.PointerStays(0), Is.False);
            Assert.That(SightLines.PointerStays(1), Is.True);
            Assert.That(SightLines.WordsOf(true), Is.EqualTo("Brackets"));
            Assert.That(SightLines.WordsOf(false), Is.EqualTo("Whole ring"));
            SightValues.PointerStays = false;
            Assert.That(SightLines.PointerStays(-1), Is.False, "none stored follows the switch");

            SightValues.On = false;
            Assert.That(TimingCues.Offered, Is.EqualTo(4), "off: the four cues of work order 134");
            Assert.That(TimingCues.Next(TimingCue.Hammer), Is.EqualTo(TimingCue.BarOnly), "round after the hammer, as before");
            Assert.That(TimingCues.Previous(TimingCue.BarOnly), Is.EqualTo(TimingCue.Hammer));
            Assert.That(TimingCues.Step(4), Is.EqualTo(TimingCue.BarOnly), "a stored sight is the default");
            Assert.That(TimingCues.InUse(TimingCue.Sight), Is.EqualTo(TimingCue.BarOnly), "and in use the bar only");
            var forward = new List<string>();
            TimingCue cue = TimingCue.BarOnly;
            for (int i = 0; i < 4; i++) { forward.Add(TimingCueLines.CueWords(cue)); cue = TimingCueLines.Stepped(cue, 1); }
            Assert.That(forward, Is.EqualTo(new[] { "Bar only", "Cylinder", "Ring", "Hammer" }));
            Assert.That(cue, Is.EqualTo(TimingCue.BarOnly));
            SettingsPages.Lines(SettingsPage.Trials, true, lines);
            Assert.That(lines, Has.No.Member(SettingLine.SightOpening), "off: no line");
            Assert.That(lines, Is.EqualTo(withoutSight), "the page as before: every other line in its place (the merge of 2026-11-27, W3, changed deliberately: the rooms' lines on the Rooms page)");
        }
    }
}
