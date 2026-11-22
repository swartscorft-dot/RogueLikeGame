using System;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 068 (his words of the evening of 2026-11-21: "healthbar og focuc er øvert, så upgrade kommer under de.
    /// syntes de også er for store. fortsatt ikke samme stil som konsept UIet"; "må tydliggjøre den combo baren"; asked,
    /// "Begge: plate oppe, små under"), as plain rules: the depths said in one place, an offer over everything of the fight's
    /// interface and the screens over the offer; the plate at the top left and the combo bar at the bottom middle on screen at
    /// 1920 by 1080 and at 1280 by 720, clear of each other, of the strip, of the map's corner and of the run box; the strokes
    /// two thirds of work order 063's width and no taller; the combo bar's line of presses from the skill's definition.
    /// </summary>
    public class InterfaceConceptTests
    {
        static InterfaceValues V => new InterfaceValues();

        static float LayoutWidth(float w, float h) => w / InterfaceRules.Scale(h, V);

        static bool Overlap(UiBox a, UiBox b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

        // ---------------- 1. The depths ----------------

        [Test]
        public void AnOffer_IsOverEveryPieceOfTheFightsInterface_AndTheScreensOverTheOffer()
        {
            // IMGUI: a lower depth is drawn later, over a higher one.
            int offer = InterfaceDepth.Levels(offerUp: true);
            foreach (int fight in new[] { InterfaceDepth.Fight, InterfaceDepth.Words, InterfaceDepth.EnemyBars, InterfaceDepth.Hud(false), InterfaceDepth.Levels(false) })
                Assert.That(offer, Is.LessThan(fight), "the offer is drawn after (over) a fight piece at " + fight);
            Assert.That(InterfaceDepth.Hud(screenUp: true), Is.LessThan(offer), "the pause menu and every screen over an offer");
            Assert.That(InterfaceDepth.Fight, Is.LessThan(InterfaceDepth.EnemyBars), "her own pieces over the enemies' bars");
            Assert.That(InterfaceDepth.FocusEdge, Is.GreaterThan(InterfaceDepth.EnemyBars), "work order 071's frame at the edges under everything");
            Assert.That(InterfaceDepth.Fight, Is.Not.EqualTo(InterfaceDepth.Offer), "never one layer: their order was chance");
        }

        // ---------------- 2. The plate and the strokes ----------------

        [Test]
        public void TheStrokes_AreTwoThirdsOf063sWidth_AndNoTaller()
        {
            InterfaceValues v = V, before = new InterfaceValues { StrokeShrink = 1f };
            UiBox now = InterfaceRules.HealthStroke(960f, 540f, 20f, v), was = InterfaceRules.HealthStroke(960f, 540f, 20f, before);
            Assert.That(now.W, Is.EqualTo(was.W * 2f / 3f).Within(1e-3f), "two thirds of the width");
            Assert.That(now.H, Is.EqualTo(was.H).Within(1e-4f), "no taller");
            UiBox focus = InterfaceRules.FocusStroke(960f, 540f, 20f, v);
            Assert.That((now.X + focus.Right) * 0.5f, Is.EqualTo(960f).Within(1e-3f), "still centred under her");
            Assert.That(v.StrokeNumber, Is.False, "no number beside them: it is on the plate");
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1280f, 720f)]
        public void ThePlate_StandsAtTheTopLeft_OnScreen_ClearOfTheMapsCornerAndTheRunBox(float w, float h)
        {
            InterfaceValues v = V;
            float width = LayoutWidth(w, h);
            UiBox whole = InterfacePlate.Whole(v), health = InterfacePlate.Health(v), focus = InterfacePlate.Focus(v);
            Assert.That(whole.Right, Is.LessThan(width * 0.4f), "in the left part of the view");
            Assert.That(whole.Bottom, Is.LessThan(v.LayoutHeight * 0.25f), "in the top quarter");
            Assert.That(health.Y, Is.GreaterThan(0f));
            Assert.That(focus.Y, Is.GreaterThanOrEqualTo(health.Bottom + v.HeavyOutline), "focus under the red plate, the outlines apart");
            // The map stands 278 of the 720 layout's pixels from the right edge, the run box at the right edge: the right part.
            float mapLeft = width - 278f * v.LayoutHeight / 720f;
            Assert.That(whole.Right, Is.LessThan(mapLeft), "clear of the map");
            Assert.That(InterfacePlate.FocusWord(v).X, Is.GreaterThan(InterfacePlate.Corner(v).Right - InterfacePlate.HalfLean(InterfacePlate.Corner(v).H, v.PlateSlant)), "the word clear of the corner's wedge");
        }

        [Test]
        public void ThePlatesNumber_FitsOnThePlate_AsTheHeavyItalicsDigitsGo()
        {
            InterfaceValues v = V;
            // "100 / 100" in the heavy condensed italic is about 0.5 of its size a figure: nine characters.
            float text = 9f * 0.5f * v.PlateNumberSize, inset = InterfacePlate.HalfLean(v.HealthPlateHeight, v.PlateSlant) + 14f;
            Assert.That(inset + text, Is.LessThan(v.HealthPlateWidth), "its widest number inside the plate");
            Assert.That(v.PlateNumberSize * 1.0f, Is.LessThan(v.HealthPlateHeight), "its height inside the plate");
        }

        // ---------------- 3. The combo bar ----------------

        [TestCase(1920f, 1080f)]
        [TestCase(1280f, 720f)]
        public void TheComboBar_IsWideAtTheBottomMiddle_OverTheStrip_OnScreen_ClearOfThePlateAndTheStrip(float w, float h)
        {
            InterfaceValues v = V;
            float width = LayoutWidth(w, h);
            UiBox bar = InterfacePlate.ComboBar(8, width, v), whole = InterfacePlate.ComboWhole(8, width, v);
            UiBox strip = InterfaceRules.WholeStrip(8, width, v), plate = InterfaceRules.StripPlate(8, width, v);
            Assert.That(bar.CenterX, Is.EqualTo(width * 0.5f).Within(0.5f), "in the middle");
            Assert.That(bar.W, Is.GreaterThan(width * 0.4f), "wide");
            Assert.That(whole.X, Is.GreaterThan(0f));
            Assert.That(whole.Right, Is.LessThan(width));
            Assert.That(whole.Bottom, Is.LessThan(plate.Y - v.Depth * InterfaceRules.Strip(v)), "over the strip's plate, clear of it");
            Assert.That(Overlap(whole, strip), Is.False, "clear of the strip");
            Assert.That(Overlap(whole, InterfacePlate.Whole(v)), Is.False, "clear of the plate");
            Assert.That(whole.Y, Is.GreaterThan(v.LayoutHeight * 0.66f), "in the bottom third, under her");
            UiBox track = InterfacePlate.ComboTrack(bar, v), label = InterfacePlate.ComboLabel(bar, v);
            Assert.That(track.X, Is.GreaterThan(label.Right), "the track right of the label");
            Assert.That(track.Right, Is.EqualTo(bar.Right).Within(1e-3f));
            // The morning of 2026-11-22: the press numbers large, standing on the track, inside what the bar covers.
            Assert.That(v.ComboNumberSize, Is.GreaterThan(v.ComboTagSize), "the numbers larger than the tag");
            foreach (float at in new[] { 0f, 0.5f, 1f })
            {
                UiBox n = InterfacePlate.ComboNumber(track, at, v);
                Assert.That(n.Bottom, Is.LessThanOrEqualTo(track.Y).And.GreaterThan(track.Y - 4f), "its foot on the track, at " + at);
                Assert.That(n.Y, Is.GreaterThanOrEqualTo(whole.Y), "inside the bar's whole, at " + at);
                Assert.That(n.X, Is.GreaterThan(label.Right - 1f), "clear of the label, at " + at);
            }
        }

        static SkillDefinition TripleTap() => new SkillDefinition { Name = "Triple Tap", RecastCount = 3, RecastWindow = 0.6f, Startup = 0.12f, CleanTapMarks = 1 };

        static SkillDefinition Backstep() => new SkillDefinition { Name = "Backstep", RecastCount = 2, RecastWindow = 0.8f, Startup = 0.2f, OpeningMove = true, CleanTapMarks = 1 };

        /// <summary>The combo bar's presses of a skill's chain, from work order 069's line, as the bar makes them.</summary>
        static ComboPress[] Presses(SkillDefinition s, KitValues kit, float wide)
        {
            RecastLine line = RecastLine.Of(s, kit, wide);
            var presses = new ComboPress[line.Count];
            for (int i = 1; i < line.Count; i++) presses[i] = ComboLine.Of(line.Beat(i + 1), ComboLine.Lead(s, kit, i + 1));
            return presses;
        }

        [Test]
        public void TheLine_GivesEveryPressAfterTheFirst_ItsWindowAndCleanPart_AsTheRulesJudgeThem()
        {
            var kit = new KitValues();
            SkillDefinition s = TripleTap();
            ComboPress[] presses = Presses(s, kit, 0f);
            Assert.That(presses.Length, Is.EqualTo(3), "every press on one line");
            for (int press = 2; press <= 3; press++)
            {
                ComboPress p = presses[press - 1];
                RecastBeat judged = RecastRhythm.Press(kit, s, press, 0f);
                Assert.That(p.Window, Is.EqualTo(judged.Window).Within(1e-6f), "press " + press + ": work order 069's window");
                Assert.That(p.Clean.From, Is.EqualTo(judged.Clean.From).Within(1e-6f), "its clean part as the judge places it");
                Assert.That(p.Clean.To, Is.EqualTo(judged.Clean.To).Within(1e-6f));
                Assert.That(p.Last, Is.EqualTo(press == 3), "only the last press is the clean tap");
                Assert.That(p.Lead, Is.EqualTo(s.Startup).Within(1e-6f), "after the startup of the shot before");
            }
        }

        [Test]
        public void TheLine_WithWideWindow_IsWidenedAsTheJudgeIs()
        {
            var kit = new KitValues();
            SkillDefinition s = TripleTap();
            ComboPress wide = Presses(s, kit, 0.5f)[2], plain = Presses(s, kit, 0f)[2];
            RecastBeat judged = RecastRhythm.Press(kit, s, 3, 0.5f);
            Assert.That(wide.Clean.From, Is.EqualTo(judged.Clean.From).Within(1e-6f));
            Assert.That(wide.Clean.To, Is.EqualTo(judged.Clean.To).Within(1e-6f));
            Assert.That(wide.Clean.Width, Is.GreaterThan(plain.Clean.Width), "wider than without");
        }

        [Test]
        public void BacksteppsLine_IsItsHopThenOnePress_AfterTheHopsOwnCleanPart()
        {
            var kit = new KitValues();
            SkillDefinition s = Backstep();
            ComboPress[] presses = Presses(s, kit, 0f);
            Assert.That(presses.Length, Is.EqualTo(2), "the hop and one press");
            CleanPart judged = RecastRhythm.Press(kit, s, 2, 0f).Clean;
            Assert.That(presses[1].Clean.From, Is.EqualTo(judged.From).Within(1e-6f), "after a hop, its own clean part (work order 064)");
            Assert.That(presses[1].Lead, Is.EqualTo(s.Startup).Within(1e-6f), "the hop's length");
            Assert.That(presses[1].Last, Is.True);
        }

        [Test]
        public void TheLine_PlacesTheComingWindows_AfterThePressBefore_TwoQuickThenABeat_AndMovesThemWhenAPressComes()
        {
            var kit = new KitValues();
            SkillDefinition s = TripleTap();
            ComboPress[] presses = Presses(s, kit, 0f);
            var opened = new[] { float.NaN, float.NaN, float.NaN };
            var pressed = new[] { 0f, float.NaN, float.NaN };
            var start = new float[3];
            float end = ComboLine.Lay(presses, 3, opened, pressed, start);
            Assert.That(start[1], Is.EqualTo(s.Startup).Within(1e-5f), "the second window after the first shot's startup");
            float aim = ComboLine.Aim(presses[1].Window, presses[1].Clean);
            Assert.That(start[2], Is.EqualTo(start[1] + aim + s.Startup).Within(1e-5f), "the third after the second pressed at its aim");
            Assert.That(end, Is.EqualTo(start[2] + presses[2].Window).Within(1e-5f), "the line ends with the last window");
            // Work order 069's rhythm as the line shows it: the second press's clean part soon after the first, the third's a beat later.
            float second = start[1] + presses[1].Clean.From, third = start[2] + presses[2].Clean.From;
            float firstGap = second, secondGap = third - (start[1] + presses[1].Clean.To);
            Assert.That(secondGap, Is.GreaterThan(firstGap), "a longer gap before the third press than before the second");
            // The second press comes late: the third window moves with it.
            opened[1] = start[1];
            pressed[1] = start[1] + 0.4f;
            ComboLine.Lay(presses, 3, opened, pressed, start);
            Assert.That(start[2], Is.EqualTo(start[1] + 0.4f + s.Startup).Within(1e-5f));
            Assert.That(ComboLine.Share(start[2], end), Is.InRange(0f, 1f));
        }

        [Test]
        public void AZone_TakesHowItsPressFell()
        {
            Assert.That(ComboLine.Of(RecastTiming.Clean), Is.EqualTo(ComboZoneState.Hit));
            Assert.That(ComboLine.Of(RecastTiming.Early), Is.EqualTo(ComboZoneState.Early));
            Assert.That(ComboLine.Of(RecastTiming.Waited), Is.EqualTo(ComboZoneState.Early), "a press that waited came too early");
            Assert.That(ComboLine.Of(RecastTiming.Late), Is.EqualTo(ComboZoneState.Late));
            Assert.That(ComboLine.Of(RecastTiming.Missed), Is.EqualTo(ComboZoneState.Missed));
            Assert.That(ComboLine.Of(RecastTiming.None), Is.Null, "not pressed yet: as it is");
        }
        [Test]
        public void TheNewValues_HaveTheirDefaults_AndTheOldLookIsReachable()
        {
            InterfaceValues v = V;
            Assert.That(v.TopPlate && v.ComboBar, Is.True, "the plate and the combo bar on");
            Assert.That(v.BladeWithCombo, Is.False, "one place to read the timing: no blade over her head beside the bar");
            var morning = new InterfaceValues { TopPlate = false, ComboBar = false, StrokeShrink = 1f, StrokeNumber = true };
            Assert.That(InterfaceRules.HealthStroke(0f, 0f, 0f, morning).W, Is.EqualTo(morning.StrokeWidth * InterfaceRules.Fight(morning)).Within(1e-4f), "063's strokes");
            Assert.That(ComboLine.Most, Is.GreaterThanOrEqualTo(5), "Steady Hand's five presses fit");
            Assert.That(v.StrokeConcept, Is.True, "the strokes in the plate's language (StrokeConcept off: 063's drawing)");
            Assert.That(v.StrokeSlant, Is.GreaterThan(v.Slant), "they lean harder than the strip's boxes, as the plate does");
        }
    }
}
