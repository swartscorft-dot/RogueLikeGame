using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The boss's entrance as rules (work order 130; Kristian, 2026-11-25: "en litt cinematic inngang, forstørre kameraet litt og lage en
    /// kul intro animasjon ... bare skal være en skikkelig tørr vits"): the switch, the timeline from its named values (the travel in,
    /// the hold, the card, the travel back), the camera's share and distance, the boss's pose ending in its fight's, the line drawn from
    /// the seed, and the card's place clear of the boss and of the interface.
    /// </summary>
    public class BossEntranceRulesTests
    {
        [Test]
        public void TheSwitch_IsOnByDefault_AndTheWholeIsAboutFourSeconds()
        {
            Assert.That(BossEntranceValues.On, Is.True, "on by default (his words)");
            var v = new BossEntranceValues();
            Assert.That(v.Total, Is.EqualTo(4f).Within(1e-5f), "the order's first guess: about four seconds in all");
            Assert.That(v.CloseDistance, Is.LessThan(CameraAngles.OursDistance), "closer than the play camera (28 m)");
            Assert.That(v.SkipGraceSeconds, Is.EqualTo(0.3f), "work order 140 (changed deliberately): a key ends it only after 0.3 s (0 in work order 130)");
        }

        [Test]
        public void TheTimeline_RunsTravelIn_Hold_TravelBack_FromItsNamedValues()
        {
            var v = new BossEntranceValues();
            float a = v.TravelInSeconds, b = a + v.HoldSeconds, c = b + v.TravelBackSeconds;
            Assert.That(BossEntranceRules.PhaseAt(-0.01f, v), Is.EqualTo(EntrancePhase.None));
            Assert.That(BossEntranceRules.PhaseAt(0f, v), Is.EqualTo(EntrancePhase.TravelIn));
            Assert.That(BossEntranceRules.PhaseAt(a - 1e-3f, v), Is.EqualTo(EntrancePhase.TravelIn));
            Assert.That(BossEntranceRules.PhaseAt(a, v), Is.EqualTo(EntrancePhase.Hold));
            Assert.That(BossEntranceRules.PhaseAt(b - 1e-3f, v), Is.EqualTo(EntrancePhase.Hold));
            Assert.That(BossEntranceRules.PhaseAt(b, v), Is.EqualTo(EntrancePhase.TravelBack));
            Assert.That(BossEntranceRules.PhaseAt(c - 1e-3f, v), Is.EqualTo(EntrancePhase.TravelBack));
            Assert.That(BossEntranceRules.PhaseAt(c, v), Is.EqualTo(EntrancePhase.Done));
            // Other values, other times: the timeline is its values'.
            var w = new BossEntranceValues { TravelInSeconds = 0.5f, HoldSeconds = 1f, TravelBackSeconds = 0.25f };
            Assert.That(w.Total, Is.EqualTo(1.75f).Within(1e-6f));
            Assert.That(BossEntranceRules.PhaseAt(0.6f, w), Is.EqualTo(EntrancePhase.Hold));
            Assert.That(BossEntranceRules.PhaseAt(1.6f, w), Is.EqualTo(EntrancePhase.TravelBack));
            Assert.That(BossEntranceRules.PhaseAt(1.75f, w), Is.EqualTo(EntrancePhase.Done));
        }

        [Test]
        public void TheCamera_LeavesHer_HoldsOnTheBoss_AndComesBackToThePlayCamera()
        {
            var v = new BossEntranceValues();
            float a = v.TravelInSeconds, b = a + v.HoldSeconds, c = b + v.TravelBackSeconds;
            Assert.That(BossEntranceRules.CameraShare(0f, v), Is.EqualTo(0f), "it starts on her");
            Assert.That(BossEntranceRules.CameraShare(a * 0.5f, v), Is.EqualTo(0.5f).Within(1e-5f), "half way at half the travel (eased both ways)");
            Assert.That(BossEntranceRules.CameraShare(a * 0.1f, v), Is.LessThan(0.1f), "eased: slow away");
            Assert.That(BossEntranceRules.CameraShare(a, v), Is.EqualTo(1f));
            Assert.That(BossEntranceRules.CameraShare((a + b) * 0.5f, v), Is.EqualTo(1f), "it holds");
            Assert.That(BossEntranceRules.CameraShare(b + v.TravelBackSeconds * 0.5f, v), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(BossEntranceRules.CameraShare(c, v), Is.EqualTo(0f), "back on her");
            Assert.That(BossEntranceRules.CameraShare(c + 10f, v), Is.EqualTo(0f));
            Assert.That(BossEntranceRules.Distance(0f, 28f, v), Is.EqualTo(28f), "the play camera's distance at 0, exactly");
            Assert.That(BossEntranceRules.Distance(1f, 28f, v), Is.EqualTo(v.CloseDistance));
            var linear = new BossEntranceValues { Eased = false };
            Assert.That(BossEntranceRules.CameraShare(linear.TravelInSeconds * 0.1f, linear), Is.EqualTo(0.1f).Within(1e-5f));
            // Every share is between 0 and 1, and the camera never jumps more than a travel's eased step allows.
            float last = 0f;
            for (float t = 0f; t <= c + 0.1f; t += 0.01f)
            {
                float s = BossEntranceRules.CameraShare(t, v);
                Assert.That(s, Is.InRange(0f, 1f));
                Assert.That(System.Math.Abs(s - last), Is.LessThan(0.03f), "no jump at " + t);
                last = s;
            }
        }

        [Test]
        public void TheCard_ShowsOnlyWhileTheCameraHoldsOnTheBoss()
        {
            var v = new BossEntranceValues();
            float a = v.TravelInSeconds, b = a + v.HoldSeconds;
            for (float t = 0f; t < a; t += 0.05f) Assert.That(BossEntranceRules.CardAlpha(t, v), Is.EqualTo(0f), "not in the travel in, " + t);
            Assert.That(BossEntranceRules.CardAlpha(a + v.CardDelay * 0.5f, v), Is.EqualTo(0f), "its delay");
            Assert.That(BossEntranceRules.CardAlpha(a + v.CardDelay + v.CardFadeIn, v), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(BossEntranceRules.CardAlpha(a + v.HoldSeconds * 0.5f, v), Is.EqualTo(1f));
            Assert.That(BossEntranceRules.CardAlpha(b - 1e-4f, v), Is.LessThan(0.01f), "gone as the hold ends");
            for (float t = b; t < b + v.TravelBackSeconds + 1f; t += 0.05f) Assert.That(BossEntranceRules.CardAlpha(t, v), Is.EqualTo(0f));
            // Fully shown for most of the hold: long enough to read a line of about fifty letters.
            int full = 0, steps = 0;
            for (float h = 0f; h < v.HoldSeconds; h += 0.01f, steps++) if (BossEntranceRules.CardAlpha(a + h, v) >= 0.999f) full++;
            Assert.That(full * 0.01f, Is.GreaterThan(1.7f), "seconds fully shown");
        }

        [Test]
        public void TheBossesEntrance_FacesTheCameraForTheCard_AndEndsInTheFightsPose()
        {
            var v = new BossEntranceValues();
            float a = v.TravelInSeconds;
            Assert.That(BossEntranceRules.MotionEnds(v), Is.LessThan(v.HoldSeconds - 0.3f), "over while the camera still holds on it");
            EntrancePose first = BossEntranceRules.PoseAt(0f, v);
            Assert.That(first.Turn, Is.EqualTo(0f));
            Assert.That(first.Lift, Is.EqualTo(0f));
            Assert.That(first.Height, Is.EqualTo(1f), "as it stood when she came in: no jump at the start");
            EntrancePose shown = BossEntranceRules.PoseAt(a + BossEntranceRules.MotionEnds(v), v);
            Assert.That(shown.Turn, Is.EqualTo(1f).Within(1e-5f), "facing the camera while the card is up");
            Assert.That(shown.ToHer, Is.EqualTo(0f));
            Assert.That(shown.Lift, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(shown.Height, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(BossEntranceRules.PoseAt(a + v.HoldSeconds - 0.01f, v).ToHer, Is.EqualTo(0f));
            Assert.That(BossEntranceRules.PoseAt(a + v.HoldSeconds + v.TravelBackSeconds * 0.5f, v).ToHer, Is.EqualTo(0.5f).Within(1e-5f), "it turns to her as the camera comes back");
            Assert.That(BossEntranceRules.PoseAt(v.Total - 1e-4f, v).ToHer, Is.GreaterThan(0.999f));
            Assert.That(BossEntranceRules.PoseAt(v.Total, v).IsFightPose, Is.True, "the fight's pose as the entrance ends");
            Assert.That(BossEntranceRules.PoseAt(v.Total + 1f, v).IsFightPose, Is.True, "after the entrance");
            Assert.That(EntrancePose.Fight.IsFightPose, Is.True);
            float lowest = 1f, highest = 0f;
            EntrancePose before = first;
            for (float t = 0f; t <= v.Total; t += 0.005f)
            {
                EntrancePose p = BossEntranceRules.PoseAt(t, v);
                lowest = System.Math.Min(lowest, p.Height);
                highest = System.Math.Max(highest, p.Lift);
                Assert.That(System.Math.Abs(p.Lift - before.Lift), Is.LessThan(0.03f), "no jump in its lift at " + t);
                Assert.That(System.Math.Abs(p.Height - before.Height), Is.LessThan(0.02f), "no jump in its height at " + t);
                Assert.That(System.Math.Abs(p.Turn - before.Turn), Is.LessThan(0.05f), "no jump in its turn at " + t);
                Assert.That(System.Math.Abs(p.ToHer - before.ToHer), Is.LessThan(0.05f), "no jump in its turn to her at " + t);
                before = p;
            }
            Assert.That(lowest, Is.EqualTo(v.CrouchSquash).Within(0.01f), "it gathers itself");
            Assert.That(highest, Is.EqualTo(v.LeapHeight).Within(0.01f), "it springs");
        }

        // ---------------- The line ----------------

        [Test]
        public void TheLine_IsOneOfTheList_TheSameForTheSameSeed_AndTheListIsDeadpan()
        {
            string[] lines = BossEntranceLines.Lines;
            Assert.That(lines.Length, Is.EqualTo(5), "the producer's five first drafts");
            foreach (string l in lines)
            {
                Assert.That(l, Does.Not.Contain("!"), "no exclamation mark: " + l);
                Assert.That(l.Trim(), Is.EqualTo(l));
                Assert.That(l.Length, Is.LessThanOrEqualTo(80), "one small line: " + l);
            }
            var seen = new Dictionary<int, int>();
            for (ulong s = 0; s < 5000; s++)
            {
                ulong seed = s * 0x9E3779B97F4A7C15UL + 130UL;
                string line = BossEntranceLines.For(seed, lines);
                Assert.That(lines, Does.Contain(line));
                Assert.That(BossEntranceLines.For(seed, lines), Is.EqualTo(line), "the same seed, the same line");
                int i = BossEntranceLines.IndexFor(seed, lines.Length);
                seen[i] = (seen.TryGetValue(i, out int n) ? n : 0) + 1;
            }
            Assert.That(seen.Keys.Count, Is.EqualTo(lines.Length), "every line is drawn");
            foreach (int n in seen.Values) Assert.That(n, Is.InRange(800, 1200), "about one run in five each");
            Assert.That(BossEntranceLines.For(1UL, new string[0]), Is.EqualTo(""));
            Assert.That(BossEntranceLines.For(1UL, null), Is.EqualTo(""));
            Assert.That(BossEntranceLines.IndexFor(7UL, 1), Is.EqualTo(0), "a list of one: that line");
            // Consecutive run seeds (as the game's fresh seeds come) do not all draw the same line.
            Assert.That(Enumerable.Range(0, 20).Select(i => BossEntranceLines.IndexFor((ulong)i, lines.Length)).Distinct().Count(), Is.GreaterThan(2));
        }

        // ---------------- The card's place ----------------

        [TestCase(1920f, 1080f)]
        [TestCase(1280f, 720f)]
        public void TheCard_StandsUnderTheBoss_ClearOfTheStripAndTheTopPlate(float width, float height)
        {
            var v = new BossEntranceValues();
            var ui = new InterfaceValues();
            float viewWidth = width / InterfaceRules.Scale(height, ui);
            UiBox card = BossEntranceRules.Whole(viewWidth, v, ui);
            // The close camera looks at the boss's feet in the screen's middle: the boss stands above it, the card below.
            double share = BossEntranceRules.BossShare(CameraAngles.OursPitch, CameraAngles.OursFieldOfView, 3.317f, v);
            Assert.That(share, Is.InRange(0.22, 0.32), "the boss about twice as tall as at 28 m (" + CameraAngles.HeightShare(55f, 30f, 28f, 3.317f).ToString("0.000") + ")");
            Assert.That(card.Y, Is.GreaterThan(ui.LayoutHeight * 0.5f + 40f), "the card's top under the boss's feet");
            UiBox strip = InterfaceRules.StripPlate(8, viewWidth, ui);
            Assert.That(card.Bottom, Is.LessThan(strip.Y), "over the skill strip");
            UiBox top = InterfacePlate.Whole(ui);
            Assert.That(card.Y, Is.GreaterThan(top.Bottom), "under the top plate");
            Assert.That(card.X, Is.GreaterThan(0f));
            Assert.That(card.Right, Is.LessThan(viewWidth));
            UiBox plate = BossEntranceRules.NamePlate(viewWidth, v), line = BossEntranceRules.LineBox(viewWidth, v);
            Assert.That(plate.CenterX, Is.EqualTo(viewWidth * 0.5f).Within(0.01f), "centred");
            Assert.That(line.Y, Is.GreaterThanOrEqualTo(plate.Bottom), "the line under the plate");
            Assert.That(v.LineSize, Is.LessThan(v.NameSize / 2), "the name large, the line small");
        }
    }
}
