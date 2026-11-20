using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 037 (Kristian's session of 2026-11-20), the two signs as rules: where the clean window's ring is at any
    /// moment of a chain, and that it is inside its band exactly when the chain itself judges a press clean, for every
    /// skill that has a clean press (Triple Tap, Backstep, Triple Tap's five in Steady Hand's window, a primed chain); the
    /// key's mark where the ring is; the full gauge's pulse and its timing; the line shown once a session.
    /// Nobody can see here whether they read well.
    /// </summary>
    public class PlayerSignRulesTests
    {
        static readonly PlayerSignValues V = new PlayerSignValues();
        static readonly KitValues Kit = new KitValues();

        /// <summary>Every skill of the defaults whose chain ends in a clean press, as a run can have it.</summary>
        static IEnumerable<TestCaseData> CleanSkills()
        {
            foreach (SkillDefinition s in SkillDefaults.Create().Where(s => s.RecastCount > 1 && RecastChain.HasCleanPress(s)))
                yield return new TestCaseData(s.Name, s.RecastCount, s.RecastWindow, s.OpeningMove, false).SetName("Ring_" + s.Name.Replace(" ", ""));
            // Steady Hand's window: Triple Tap two presses longer, its clean press the fifth (work order 025).
            SkillDefinition tap = FocusModeRules.InWindow(SkillDefaults.Create().First(s => s.Name == "Triple Tap"), new FocusModeValues());
            yield return new TestCaseData(tap.Name, tap.RecastCount, tap.RecastWindow, tap.OpeningMove, false).SetName("Ring_TripleTapInSteadyHand");
            // Primed by Hair Trigger (work order 025): the whole window is clean.
            yield return new TestCaseData("Triple Tap", 3, 0.6f, false, true).SetName("Ring_TripleTapPrimed");
        }

        [Test]
        public void TheSkillsWithACleanPress_AreTripleTapAndBackstep()
        {
            Assert.That(SkillDefaults.Create().Where(s => s.RecastCount > 1 && RecastChain.HasCleanPress(s)).Select(s => s.Name),
                        Is.EquivalentTo(new[] { "Triple Tap", "Backstep" }));
        }

        [TestCaseSource(nameof(CleanSkills))]
        public void TheRing_IsInsideItsBand_ExactlyWhenTheChainJudgesAPressClean(string name, int count, float window, bool opening, bool primed)
        {
            var chain = new RecastChain();
            const int slot = 7;
            float now = 10f;
            chain.Start(slot, count, window, opening);
            // Every press but the last, each in its window: no ring for those (their next press cannot be clean).
            for (int press = 2; press < count; press++)
            {
                chain.ShotWentOff(now);
                Assert.That(chain.NextIsLast, Is.False);
                now += 0.1f;
                chain.Recast();
                now += 0.15f;
            }
            Assert.That(chain.NextIsLast, Is.True, "the next press is the last");
            float share = primed ? 1f : Kit.CleanShare;

            // Before the shot before has gone off: the ring stands at its start, out of the band, and the press waits.
            Assert.That(chain.SinceOpened(now), Is.EqualTo(-1f));
            Assert.That(CleanWindowSign.Shows(true, share, chain.Window, chain.SinceOpened(now)), Is.True, "up already");
            Assert.That(CleanWindowSign.RingRadius(chain.SinceOpened(now), window, share, V), Is.EqualTo(CleanWindowSign.StartRadius(share, V)));
            Assert.That(chain.Judge(slot, now, Kit.CleanShare, true), Is.EqualTo(RecastPress.Early));

            float opened = now;
            chain.ShotWentOff(opened);
            Assert.That(chain.Window, Is.EqualTo(window));
            float last = float.PositiveInfinity;
            int clean = 0, plain = 0;
            for (float since = 0f; since <= window + 1e-4f; since += 0.0037f)
            {
                float t = opened + since;
                float r = CleanWindowSign.RingRadius(chain.SinceOpened(t), window, share, V);
                RecastPress judged = chain.Judge(slot, t, Kit.CleanShare, true);
                if (primed) judged = FocusModeRules.Primed(judged, chain.NextIsLast, true);
                bool isClean = judged == RecastPress.Clean;
                bool inBand = r <= V.BandOuter + 1e-4f && r >= V.BandInner - 1e-4f;
                // Away from the edge by more than the clock's rounding, the two agree.
                float edge = window * (1f - share);
                if (System.Math.Abs(since - edge) > 2e-4f && window - since > 2e-4f)
                {
                    Assert.That(inBand, Is.EqualTo(isClean), name + " at " + since.ToString("0.0000") + " s: r " + r);
                    Assert.That(CleanWindowSign.InBand(chain.SinceOpened(t), window, share), Is.EqualTo(isClean), "InBand at " + since);
                }
                Assert.That(r, Is.LessThanOrEqualTo(last + 1e-6f), "it only closes");
                Assert.That(r, Is.GreaterThanOrEqualTo(V.BandInner - 1e-5f), "never inside the band's inner edge");
                last = r;
                if (isClean) clean++;
                else plain++;
            }
            Assert.That(clean, Is.GreaterThan(0), "a clean part");
            if (primed) Assert.That(plain, Is.EqualTo(0), "primed: all of the window");
            else Assert.That(plain, Is.GreaterThan(0), "and a plain part before it");
            // At the edges.
            float cleanFrom = window * (1f - share);
            Assert.That(CleanWindowSign.RingRadius(cleanFrom, window, share, V), Is.EqualTo(V.BandOuter).Within(1e-4f), "at the band's outer edge as the clean part opens");
            Assert.That(CleanWindowSign.RingRadius(window, window, share, V), Is.EqualTo(V.BandInner).Within(1e-4f), "at its inner edge as the window runs out");
            Assert.That(CleanWindowSign.Shows(true, share, window, window + 0.01f), Is.False, "gone once the window has run out");
            Assert.That(chain.Judge(slot, opened + window + 0.01f, Kit.CleanShare, true), Is.EqualTo(RecastPress.None), "and so is the chain");
        }

        [Test]
        public void TheRing_WithTodaysNumbers_StartsAt205cm_AndClosesAtOneSpeed()
        {
            // Window 0.6 s, its last third clean: the ring starts 3 band widths out and closes 1.05 m in 0.6 s.
            float share = Kit.CleanShare, window = 0.6f;
            float start = CleanWindowSign.StartRadius(share, V);
            Assert.That(start, Is.EqualTo(V.BandInner + (V.BandOuter - V.BandInner) / share).Within(1e-4f));
            Assert.That(start, Is.EqualTo(2.05f).Within(1e-3f));
            float speed1 = (CleanWindowSign.RingRadius(0.1f, window, share, V) - CleanWindowSign.RingRadius(0.2f, window, share, V)) / 0.1f;
            float speed2 = (CleanWindowSign.RingRadius(0.45f, window, share, V) - CleanWindowSign.RingRadius(0.55f, window, share, V)) / 0.1f;
            Assert.That(speed1, Is.EqualTo(speed2).Within(1e-3f), "one speed through the band and before it");
            Assert.That(speed1, Is.EqualTo(1.75f).Within(1e-3f), "1.75 m a second");
        }

        [Test]
        public void TheRing_HeldInByItsMost_StillMeetsTheBandsEdgesOnTime()
        {
            var v = new PlayerSignValues { MostRadius = 2f };
            const float share = 0.1f, window = 0.6f;
            Assert.That(CleanWindowSign.StartRadius(share, v), Is.EqualTo(2f).Within(1e-5f), "held in");
            Assert.That(CleanWindowSign.RingRadius(window * 0.9f, window, share, v), Is.EqualTo(v.BandOuter).Within(1e-4f));
            Assert.That(CleanWindowSign.RingRadius(window * 0.95f, window, share, v), Is.InRange(v.BandInner, v.BandOuter));
            Assert.That(CleanWindowSign.RingRadius(window * 0.85f, window, share, v), Is.GreaterThan(v.BandOuter));
        }

        [Test]
        public void TheKeysMark_IsWhereTheRingIs_AndItsBand_TheCleanShare()
        {
            float share = Kit.CleanShare, window = 0.6f;
            for (float since = -0.1f; since <= window; since += 0.01f)
            {
                float r = CleanWindowSign.RingRadius(since, window, share, V);
                Assert.That(CleanWindowSign.MarkOf(r, share, V), Is.EqualTo(CleanWindowSign.MarkShare(since, window)).Within(1e-4f),
                            "the mark is the share of the window left, at " + since);
            }
            Assert.That(CleanWindowSign.MarkOf(V.BandOuter, share, V), Is.EqualTo(share).Within(1e-4f), "the key's band: the clean share at its end");
            Assert.That(CleanWindowSign.MarkOf(V.BandOuter, 1f, V), Is.EqualTo(1f).Within(1e-4f), "primed: all of it");
        }

        [Test]
        public void NoRing_WithoutAChain_ACleanPress_OrAWindow()
        {
            Assert.That(CleanWindowSign.Shows(false, 0.33f, 0.6f, 0.1f), Is.False, "no chain");
            Assert.That(CleanWindowSign.Shows(true, 0f, 0.6f, 0.1f), Is.False, "the next press cannot be clean");
            Assert.That(CleanWindowSign.Shows(true, 0.33f, 0f, 0.1f), Is.False, "no window");
            Assert.That(CleanWindowSign.Shows(true, 0.33f, 0.6f, -1f), Is.True, "before it opens: up, waiting");
            Assert.That(CleanWindowSign.Line, Is.EqualTo("Press again inside the band: a clean press"));
        }

        // ---------------- The pulse of a full gauge ----------------

        [Test]
        public void ThePulse_ComesEveryOneAndAHalfSeconds_AfterTheFlash()
        {
            Assert.That(V.PulseSeconds, Is.EqualTo(1.5f));
            Assert.That(FocusSign.Phase(0f, V), Is.EqualTo(-1f), "not while the flash has the moment");
            Assert.That(FocusSign.Phase(V.PulseDelay - 0.01f, V), Is.EqualTo(-1f));
            Assert.That(FocusSign.Phase(V.PulseDelay, V), Is.EqualTo(0f).Within(1e-5f), "the first starts after the delay");
            Assert.That(FocusSign.Phase(V.PulseDelay + 0.75f, V), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(FocusSign.Phase(V.PulseDelay + 1.5f + 0.3f, V), Is.EqualTo(0.2f).Within(1e-4f), "and again a period later");
            Assert.That(FocusSign.Phase(V.PulseDelay + 60f * 1.5f + 0.15f, V), Is.EqualTo(0.1f).Within(1e-3f), "a minute and a half on, the same beat");

            // Starts counted over a minute: about forty.
            int starts = 0;
            float before = -1f;
            for (float t = 0f; t < 60f; t += 0.01f)
            {
                float p = FocusSign.Phase(t, V);
                if (p >= 0f && (before < 0f || p < before)) starts++;
                before = p;
            }
            Assert.That(starts, Is.InRange(39, 41), "once every 1.5 s");
        }

        [Test]
        public void ThePulse_GrowsThinAndCalm_StepsDownInBrightness_AndIsNotDrawnTheRestOfItsBeat()
        {
            Assert.That(FocusSign.RingShown(0f, V), Is.True);
            Assert.That(FocusSign.RingShown(V.PulseShowShare - 0.01f, V), Is.True);
            Assert.That(FocusSign.RingShown(V.PulseShowShare + 0.01f, V), Is.False, "the rest of the beat: nothing on the floor");
            Assert.That(FocusSign.RingShown(-1f, V), Is.False);
            Assert.That(FocusSign.RingRadius(0f, V), Is.EqualTo(V.PulseFrom).Within(1e-5f));
            Assert.That(FocusSign.RingRadius(V.PulseShowShare, V), Is.EqualTo(V.PulseTo).Within(1e-5f));
            float r = 0f;
            int level = 0;
            for (float p = 0f; p < V.PulseShowShare; p += 0.01f)
            {
                Assert.That(FocusSign.RingRadius(p, V), Is.GreaterThanOrEqualTo(r), "it only grows");
                Assert.That(FocusSign.Level(p, V), Is.GreaterThanOrEqualTo(level), "and only fades");
                r = FocusSign.RingRadius(p, V);
                level = FocusSign.Level(p, V);
            }
            Assert.That(level, Is.EqualTo(2));
            Assert.That(FocusSign.Level(0.9f, V), Is.EqualTo(-1));
        }

        [Test]
        public void TheGauge_PulsesBetweenItsTwoOpacities_OnTheSameBeat()
        {
            Assert.That(FocusSign.GaugeAlpha(0f, V), Is.EqualTo(V.GaugeLow).Within(1e-4f));
            Assert.That(FocusSign.GaugeAlpha(0.75f, V), Is.EqualTo(V.GaugeHigh).Within(1e-4f), "half a beat: brightest");
            Assert.That(FocusSign.GaugeAlpha(1.5f, V), Is.EqualTo(V.GaugeLow).Within(1e-3f), "a beat: low again");
            for (float t = 0f; t < 10f; t += 0.05f)
                Assert.That(FocusSign.GaugeAlpha(t, V), Is.InRange(V.GaugeLow - 1e-4f, V.GaugeHigh + 1e-4f));
        }

        [Test]
        public void TheWord_IsFOCUSWithTheBoundKey()
        {
            Assert.That(FocusSign.Word("Z"), Is.EqualTo("FOCUS  Z"));
            Assert.That(FocusSign.Word("X"), Is.EqualTo("FOCUS  X"), "whatever it is bound to");
            Assert.That(FocusSign.Word(""), Is.EqualTo("FOCUS"));
        }

        // ---------------- Once a session ----------------

        [Test]
        public void TheLine_ShowsForItsSeconds_TheFirstTimeOnly()
        {
            var once = new OncePerSession();
            Assert.That(once.Showing(0f), Is.False);
            Assert.That(once.TryShow(5f, 4f), Is.True, "the first time");
            Assert.That(once.Showing(5f), Is.True);
            Assert.That(once.Showing(8.9f), Is.True);
            Assert.That(once.Showing(9.01f), Is.False, "a few seconds, then gone");
            Assert.That(once.TryShow(20f, 4f), Is.False, "never twice");
            Assert.That(once.Showing(21f), Is.False);
            Assert.That(once.TryShow(6f, 4f), Is.False, "not even while it shows");
            once.Forget();
            Assert.That(once.TryShow(30f, 4f), Is.True, "a new session");
        }

        [Test]
        public void TheSwitches_AreOnByDefault()
        {
            Assert.That(V.CleanWindow, Is.False, "the ring: off by default since work order 040"); // changed deliberately for work order 040
            Assert.That(V.KeyStrip, Is.True, "the key's strip and the tick stay");
            Assert.That(V.FocusFull, Is.True);
        }

        [Test]
        public void TheChain_TellsItsWindow_AndTheTimeSinceItOpened_AndChangesNothing()
        {
            var chain = new RecastChain();
            Assert.That(chain.Window, Is.EqualTo(0f));
            Assert.That(chain.SinceOpened(3f), Is.EqualTo(-1f));
            chain.Start(2, 3, 0.6f);
            Assert.That(chain.Window, Is.EqualTo(0.6f));
            Assert.That(chain.SinceOpened(3f), Is.EqualTo(-1f), "the first shot has not gone off");
            chain.ShotWentOff(3f);
            Assert.That(chain.SinceOpened(3.25f), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(chain.SinceOpened(3.7f), Is.EqualTo(0.7f).Within(1e-5f), "past its length once run out");
            Assert.That(chain.Judge(2, 3.25f, 1f / 3f, true), Is.EqualTo(RecastPress.Shot), "asked, the chain is as it was");
            chain.End();
            Assert.That(chain.Window, Is.EqualTo(0f));
            Assert.That(chain.SinceOpened(3.25f), Is.EqualTo(-1f));
        }
    }
}
