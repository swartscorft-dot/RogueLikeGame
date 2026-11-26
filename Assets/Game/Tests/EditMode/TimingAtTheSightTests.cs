using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 134, the timing at the sight, as rules: for every timed skill of hers (every skill with a recast chain and a
    /// clean press, Triple Tap and Backstep, and their chains as upgrades and focus make them: Fourth Tap, Steady Hand's five, Hair
    /// Trigger's primed last press, Wide window's widened parts, the rhythm's switch off), each cue's shown clean part equals the
    /// judge's at every sampled time: the cylinder's chamber in the notch, the ring inside the band, the hammer at its brightest,
    /// and never one of them outside it, before the window opens too. The chain's three presses (three chambers, the later ones
    /// waiting; one chamber for Backstep; the turn longer for the longer wait; the chamber before clear of the notch; the click a
    /// step up). The setting's stepping and its stored values; the order's switch off.
    /// </summary>
    public class TimingAtTheSightTests
    {
        const int Slot = 7;
        const float Sample = 0.001f;     // s between sampled times: every millisecond of every window
        const float Before = 0.25f;      // s sampled before a window opens (the shot before in its startup)
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly TimingAtTheSightValues V = new TimingAtTheSightValues();

        static SkillDefinition Skill(string name) => SkillDefaults.Create().First(s => s.Name == name);

        /// <summary>One chain to judge: a skill as she may have it, her Wide window, Hair Trigger's priming, the kit.</summary>
        public sealed class Case
        {
            public string Name;
            public SkillDefinition Skill;
            public float Wide;
            public bool Primed;
            public KitValues Kit = new KitValues();
            public override string ToString() => Name;
        }

        static IEnumerable<Case> TimedChains()
        {
            // Every skill of hers that has a timed press: listed from the skills as they ship, not by name.
            foreach (SkillDefinition s in SkillDefaults.Create().Where(s => s.RecastCount > 1 && RecastChain.HasCleanPress(s)))
            {
                yield return new Case { Name = s.Name, Skill = s };
                yield return new Case { Name = s.Name + " widened", Skill = s, Wide = 0.5f };
                yield return new Case { Name = s.Name + " primed", Skill = s, Primed = true };
                yield return new Case { Name = s.Name + " rhythm off", Skill = s, Kit = new KitValues { ChainRhythm = false } };
            }
            SkillDefinition four = Skill("Triple Tap");
            four.RecastCount = 4;
            yield return new Case { Name = "Triple Tap with Fourth Tap", Skill = four };
            yield return new Case { Name = "Triple Tap in Steady Hand's window", Skill = FocusModeRules.InWindow(Skill("Triple Tap"), new FocusModeValues()) };
        }

        static IEnumerable<TestCaseData> Chains() => TimedChains().Select(c => new TestCaseData(c).SetName("EveryCue_SaysCleanExactlyWhenTheJudgeDoes_" + c.Name.Replace(' ', '_').Replace("'", "")));

        [Test]
        public void TheTimedSkills_AreTripleTapAndBackstep_AsTheyShip()
        {
            Assert.That(SkillDefaults.Create().Where(s => s.RecastCount > 1 && RecastChain.HasCleanPress(s)).Select(s => s.Name),
                        Is.EquivalentTo(new[] { "Triple Tap", "Backstep" }), "every skill with a recast window and a clean press");
        }

        /// <summary>
        /// The press's clean part as the character's ChainCleanPart gives it (work order 069's beat, widened by Wide window), and
        /// Hair Trigger's priming of the last press (the whole window): what <c>RecastCleanPart</c> hands the cues.
        /// </summary>
        static CleanPart ShownPart(Case c, int press, out float window)
        {
            RecastBeat beat = RecastRhythm.Press(c.Kit, c.Skill.RecastWindow, c.Skill.OpeningMove, press, c.Skill.RecastCount, c.Wide);
            window = beat.Window;
            bool shown = beat.Last || c.Kit.ChainRhythm; // the press shown: every one with the rhythm on, only the last with it off
            if (!shown) return default;
            return c.Primed && beat.Last ? CleanPart.Whole(beat.Window) : beat.Clean;
        }

        [TestCaseSource(nameof(Chains))]
        public void EveryCue_SaysCleanExactlyWhenTheJudgeDoes(Case c)
        {
            SkillDefinition skill = c.Skill;
            var chain = new RecastChain();
            float now = 10f;
            chain.Start(Slot, skill.RecastCount, skill.RecastWindow, skill.OpeningMove);
            chain.NextWindow(RecastRhythm.Press(c.Kit, skill, 2, c.Wide).Window);
            int chambers = CylinderCue.Chambers(skill.RecastCount, skill.OpeningMove);
            float spacing = CylinderCue.Spacing(chambers, V);
            float reach = 0f;
            for (int press = 2; press <= skill.RecastCount; press++)
            {
                CleanPart part = ShownPart(c, press, out _);
                if (part.Any) reach = Math.Max(reach, CylinderCue.Reach(part, ComboLine.Lead(skill, c.Kit, press)));
            }
            float notch = CylinderCue.Notch(spacing, reach, V);
            int sampled = 0, clean = 0;
            for (int press = 2; press <= skill.RecastCount; press++)
            {
                CleanPart part = ShownPart(c, press, out float window);
                float lead = ComboLine.Lead(skill, c.Kit, press);
                // Before the window opens: the cues may already move toward the clean part, and none of them says clean.
                for (float before = 0f; before <= Before; before += Sample)
                {
                    float since = TimingCueClock.Since(chain.SinceOpened(now), lead - before, lead);
                    Assert.That(since, Is.LessThan(0f), "before the window opens the cues' clock is below 0");
                    Assert.That(CylinderCue.SaysClean(chain.SinceOpened(now), window, part, notch), Is.False);
                    if (part.Any)
                    {
                        Assert.That(CylinderCue.InNotch(CylinderCue.LiveAngle(since, part, notch), notch, CylinderCue.Speed(part, notch)), Is.False,
                                    c + ", press " + press + ": no chamber in the notch " + before.ToString("0.000", Inv) + " s before its window opens");
                        Assert.That(RingCue.InBand(RingCue.Radius(since, part, V), RingCue.Speed(part, V), V), Is.False, "no ring in the band before its window");
                    }
                    Assert.That(HammerCue.Level(-1f, window, part, V), Is.EqualTo(0f), "the hammer dark before its window");
                }
                Assert.That(chain.Judge(Slot, now, part, true), Is.EqualTo(RecastPress.Early), "the window is not open yet");

                float opened = now;
                chain.ShotWentOff(opened);
                float pressAt = -1f;
                for (float since = 0f; since <= window + 0.05f; since += Sample)
                {
                    float t = opened + since;
                    float s = chain.SinceOpened(t);
                    bool open = chain.IsWindowOpen(t);
                    // The judge, as the character asks it (TimingOfRecast): the last press clean, a press before it in its clean part.
                    bool judged;
                    if (chain.NextIsLast)
                    {
                        RecastPress p = chain.Judge(Slot, t, part, RecastChain.HasCleanPress(skill));
                        if (c.Primed) p = FocusModeRules.Primed(p, chain.NextIsLast, true);
                        judged = p == RecastPress.Clean;
                    }
                    else judged = chain.InCleanPart(Slot, t, part);
                    bool shown = part.Any;
                    judged &= shown && open; // what RecastPressWouldBeClean says: only for a press shown (the rhythm off: the last)
                    bool cyl = CylinderCue.SaysClean(open ? s : -1f, window, part, notch);
                    bool ring = RingCue.SaysClean(open ? s : -1f, window, part, V);
                    bool hammer = HammerCue.SaysClean(open ? s : -1f, window, part, V);
                    string at = c + ", press " + press + " at " + since.ToString("0.000", Inv) + " s";
                    Assert.That(cyl, Is.EqualTo(judged), at + ": the chamber in the notch exactly while the press is clean");
                    Assert.That(ring, Is.EqualTo(judged), at + ": the ring inside the band exactly while the press is clean");
                    Assert.That(hammer, Is.EqualTo(judged), at + ": the muzzles at their brightest exactly while the press is clean");
                    sampled++;
                    if (judged)
                    {
                        clean++;
                        if (pressAt < 0f) pressAt = since;
                    }
                }
                if (part.Any) Assert.That(pressAt, Is.GreaterThanOrEqualTo(0f), c + ", press " + press + ": a clean moment was sampled");
                // The press, in the middle of its clean part where there is one, and on to the next.
                float pressSince = part.Any ? (part.From + part.To) * 0.5f : window * 0.5f;
                now = opened + pressSince;
                chain.Recast(true);
                if (!chain.IsComplete) chain.NextWindow(RecastRhythm.Press(c.Kit, skill, chain.Shots + 1, c.Wide).Window);
                now += lead;
            }
            TestContext.WriteLine(string.Format(Inv, "TIMINGCUE {0}: {1} presses, {2} chambers {3:0} degrees apart, notch {4:0.0} degrees; {5} sampled times, {6} clean", c,
                                                skill.RecastCount - 1, chambers, spacing, notch, sampled, clean));
            Assert.That(clean, Is.GreaterThan(0));
        }

        // ---------------- The chain's three presses ----------------

        [Test]
        public void TripleTap_ThreeChambers_TheLaterOnesWaiting_Backstep_One()
        {
            SkillDefinition tap = Skill("Triple Tap"), back = Skill("Backstep");
            Assert.That(CylinderCue.Chambers(tap.RecastCount, tap.OpeningMove), Is.EqualTo(3), "three presses, three shots: three chambers");
            Assert.That(CylinderCue.ChamberOf(1, false), Is.EqualTo(0), "the first shot's");
            Assert.That(CylinderCue.ChamberOf(2, false), Is.EqualTo(1));
            Assert.That(CylinderCue.ChamberOf(3, false), Is.EqualTo(2));
            Assert.That(CylinderCue.Chambers(back.RecastCount, back.OpeningMove), Is.EqualTo(1), "the hop fires nothing: one chamber, the shot's");
            Assert.That(CylinderCue.ChamberOf(1, true), Is.EqualTo(-1), "the hop has none");
            Assert.That(CylinderCue.ChamberOf(2, true), Is.EqualTo(0));
            Assert.That(CylinderCue.Spacing(3, V), Is.EqualTo(120f));
            Assert.That(CylinderCue.Spacing(1, V), Is.EqualTo(120f), "one chamber turns as one of three");
            // The later chambers wait behind the one waited for: farther from the notch, on the side it comes from.
            float turn = CylinderCue.Turn(-30f, 1, 120f);
            Assert.That(CylinderCue.AngleOf(1, turn, 120f), Is.EqualTo(-30f).Within(1e-4f));
            Assert.That(CylinderCue.AngleOf(2, turn, 120f), Is.EqualTo(-150f).Within(1e-4f), "the third waits a spacing behind");
            Assert.That(CylinderCue.AngleOf(0, turn, 120f), Is.EqualTo(90f).Within(1e-4f), "the first, fired, a spacing on past the notch");
        }

        [Test]
        public void TheCylinder_ALongerWaitIsALongerTurn_AndTheChamberBeforeIsClearOfTheNotch()
        {
            var kit = new KitValues();
            SkillDefinition tap = Skill("Triple Tap");
            CleanPart second = RecastRhythm.Press(kit, tap, 2, 0f).Clean, third = RecastRhythm.Press(kit, tap, 3, 0f).Clean;
            float lead2 = ComboLine.Lead(tap, kit, 2), lead3 = ComboLine.Lead(tap, kit, 3);
            float reach = Math.Max(CylinderCue.Reach(second, lead2), CylinderCue.Reach(third, lead3));
            float spacing = CylinderCue.Spacing(3, V);
            float notch = CylinderCue.Notch(spacing, reach, V);
            Assert.That(notch, Is.EqualTo(V.NotchMost), "Triple Tap's notch at its widest");
            // The degrees turned from the window's opening to the clean part: the last press waits a beat, so its turn is longer.
            float turned2 = CylinderCue.LiveAngle(second.From, second, notch) - CylinderCue.LiveAngle(0f, second, notch);
            float turned3 = CylinderCue.LiveAngle(third.From, third, notch) - CylinderCue.LiveAngle(0f, third, notch);
            Assert.That(turned3, Is.GreaterThan(turned2 * 2f), "the third press's turn into the notch is the longer: " + turned2 + " and " + turned3 + " degrees");
            Assert.That(CylinderCue.Speed(third, notch), Is.EqualTo(notch / third.Width).Within(1e-3f), "the speed from the window's own times");
            // When a press's turn begins (the press before it), the chamber before stands clear of the notch.
            foreach ((CleanPart part, float lead) in new[] { (second, lead2), (third, lead3) })
            {
                float before = CylinderCue.AngleOf(0, CylinderCue.Turn(CylinderCue.LiveAngle(-lead, part, notch), 1, spacing), spacing);
                Assert.That(before - notch * 0.5f, Is.GreaterThanOrEqualTo(V.NotchClear - 1e-3f), "the chamber before is out of the notch");
            }
            TestContext.WriteLine(string.Format(Inv, "TIMINGCUE Triple Tap's cylinder: notch {0:0.0} degrees; press 2 turns {1:0.0} degrees a second, {2:0.0} degrees from its window's opening to its clean part; press 3 {3:0.0} a second, {4:0.0} degrees",
                                                notch, CylinderCue.Speed(second, notch), turned2, CylinderCue.Speed(third, notch), turned3));
        }

        [Test]
        public void TheRing_ClosesThroughTheBand_AtTheCleanPartsOwnSpeed_AndStartsFartherOutForALongerWait()
        {
            var kit = new KitValues();
            SkillDefinition tap = Skill("Triple Tap");
            CleanPart second = RecastRhythm.Press(kit, tap, 2, 0f).Clean, third = RecastRhythm.Press(kit, tap, 3, 0f).Clean;
            Assert.That(RingCue.Radius(second.From, second, V), Is.EqualTo(V.BandOuter).Within(1e-4f), "at the band's outer edge as the clean part opens");
            Assert.That(RingCue.Radius(second.To, second, V), Is.EqualTo(V.BandInner).Within(1e-4f), "at its inner edge as it ends");
            Assert.That(RingCue.Radius(5f, second, V), Is.EqualTo(V.RingLeast), "on in no further than its least");
            Assert.That(RingCue.Radius(0f, third, V), Is.GreaterThan(RingCue.Radius(0f, second, V)), "the beat's ring starts farther out");
            Assert.That(V.BandInner, Is.GreaterThan(V.RingLeast), "the inside is left open");
            Assert.That(RingCue.Shows(V.RingMost + 1f, V), Is.EqualTo(0f));
            Assert.That(RingCue.Shows(V.BandOuter, V), Is.EqualTo(1f));
        }

        [Test]
        public void TheHammer_StepsUpTowardTheCleanPart_BrightestOnlyInIt_FaintAfter()
        {
            var kit = new KitValues();
            CleanPart third = RecastRhythm.Press(kit, Skill("Triple Tap"), 3, 0f).Clean;
            float window = RecastRhythm.Press(kit, Skill("Triple Tap"), 3, 0f).Window;
            float last = 0f;
            int steps = 0;
            for (float t = 0f; t < third.From - 1e-4f; t += Sample)
            {
                float level = HammerCue.Level(t, window, third, V);
                Assert.That(level, Is.GreaterThan(0f).And.LessThan(HammerCue.Brightest), "lit, under the brightest, before the clean part");
                Assert.That(level, Is.GreaterThanOrEqualTo(last), "it only steps up");
                if (level > last) steps++;
                last = level;
            }
            Assert.That(steps, Is.EqualTo(V.HammerSteps), "in its steps");
            Assert.That(HammerCue.Level((third.From + third.To) * 0.5f, window, third, V), Is.EqualTo(HammerCue.Brightest));
            Assert.That(HammerCue.Level(third.To + 0.01f, window, third, V), Is.LessThan(V.HammerStepLevel + 1e-6f), "faint after");
            Assert.That(HammerCue.Level(window + 0.01f, window, third, V), Is.EqualTo(0f), "dark when the window has run out");
            Assert.That(V.HammerSteps * V.HammerStepLevel, Is.LessThan(HammerCue.Brightest), "no step reaches the brightest");
        }

        [Test]
        public void TheClick_AHammerCocked_MadeInCode_AStepUpEachPress()
        {
            float[] samples = Synth.Render(TimingClick.Recipe(), Synth.SampleRate);
            Assert.That(samples.Length, Is.EqualTo((int)Math.Round(0.07 * Synth.SampleRate)), "70 ms");
            Assert.That(samples.Max(x => Math.Abs(x)), Is.LessThanOrEqualTo(Synth.MostPeak), "never at full scale");
            Assert.That(samples[0], Is.EqualTo(0f));
            Assert.That(samples[samples.Length - 1], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(samples.All(x => !float.IsNaN(x)), Is.True);
            Assert.That(TimingClick.Pitch(2, V), Is.EqualTo(V.ClickPitch), "the first recast press at its own pitch");
            Assert.That(TimingClick.Pitch(3, V), Is.EqualTo(V.ClickPitch * (float)Math.Pow(2.0, V.ClickStep / 12.0)).Within(1e-5f), "the next a step up");
            Assert.That(TimingClick.Pitch(4, V), Is.GreaterThan(TimingClick.Pitch(3, V)));
            Assert.That(V.ClickVolume, Is.GreaterThan(0f).And.LessThan(0.4f), "its own level, quiet under the shots (0.4 to 0.9)");
        }

        // ---------------- The setting and the switch ----------------

        [Test]
        public void TheSetting_FourCues_BarOnlyByDefault_SteppingRoundAndBack_TheClickOnWithAnyCueButTheBar()
        {
            Assert.That(TimingCues.Names, Is.EqualTo(new[] { "Bar only", "Cylinder", "Ring", "Hammer" }), "the order's values");
            Assert.That(TimingCues.Count, Is.EqualTo(Enum.GetValues(typeof(TimingCue)).Length));
            Assert.That(TimingCues.Default, Is.EqualTo(TimingCue.BarOnly), "today's, until he has chosen");
            Assert.That(TimingCues.Next(TimingCue.BarOnly), Is.EqualTo(TimingCue.Cylinder));
            Assert.That(TimingCues.Next(TimingCue.Cylinder), Is.EqualTo(TimingCue.Ring));
            Assert.That(TimingCues.Next(TimingCue.Ring), Is.EqualTo(TimingCue.Hammer));
            Assert.That(TimingCues.Next(TimingCue.Hammer), Is.EqualTo(TimingCue.BarOnly), "and round");
            Assert.That(TimingCues.Previous(TimingCue.BarOnly), Is.EqualTo(TimingCue.Hammer));
            Assert.That(TimingCues.Step(7), Is.EqualTo(TimingCue.BarOnly), "a stored value that is no cue: the default");
            Assert.That(TimingCues.Step(-1), Is.EqualTo(TimingCue.BarOnly));
            Assert.That(TimingCues.Step(2), Is.EqualTo(TimingCue.Ring), "a stored cue comes back as it was saved");
            Assert.That(TimingCues.StoredSound(5), Is.EqualTo(TimingCues.SoundNotStored));
            Assert.That(TimingCues.Sound(TimingCues.SoundNotStored, TimingCue.BarOnly), Is.False, "the bar only: no click unless he turns it on");
            foreach (TimingCue cue in new[] { TimingCue.Cylinder, TimingCue.Ring, TimingCue.Hammer })
            {
                Assert.That(TimingCues.Sound(TimingCues.SoundNotStored, cue), Is.True, cue + ": the click on by default");
                Assert.That(TimingCues.Sound(TimingCues.SoundOff, cue), Is.False, cue + ": off when he turned it off");
            }
            Assert.That(TimingCues.Sound(TimingCues.SoundOn, TimingCue.BarOnly), Is.True, "the click alone, with the bar");
        }

        [Test]
        public void TheSwitch_OnByDefault_Off_TheBarOnlyAndNoClickWhateverIsStored()
        {
            Assert.That(TimingAtTheSightValues.On, Is.True, "on with his words");
            bool was = TimingAtTheSightValues.On;
            try
            {
                TimingAtTheSightValues.On = false;
                foreach (TimingCue cue in Enum.GetValues(typeof(TimingCue)))
                {
                    Assert.That(TimingCues.InUse(cue), Is.EqualTo(TimingCue.BarOnly), cue + " stored: the bar only");
                    Assert.That(TimingCues.SoundInUse(TimingCues.SoundOn, cue), Is.False, "no click");
                }
            }
            finally { TimingAtTheSightValues.On = was; }
            Assert.That(TimingCues.InUse(TimingCue.Ring), Is.EqualTo(TimingCue.Ring));
        }

        [Test]
        public void TheClock_BeforeTheWindowNeverReaches0_InItIsTheJudgesOwn()
        {
            Assert.That(TimingCueClock.Since(0.25f, 0.5f, 0.15f), Is.EqualTo(0.25f), "in the window: the judge's seconds");
            Assert.That(TimingCueClock.Since(0f, 0.15f, 0.15f), Is.EqualTo(0f));
            Assert.That(TimingCueClock.Since(-1f, 0f, 0.15f), Is.EqualTo(-0.15f).Within(1e-6f), "at the press: its startup before");
            Assert.That(TimingCueClock.Since(-1f, 0.3f, 0.15f), Is.LessThan(0f), "a startup run long (a hit stop): still before");
            Assert.That(TimingCueClock.Since(-1f, 0.1f, 0.15f), Is.GreaterThan(TimingCueClock.Since(-1f, 0.05f, 0.15f)), "it runs on");
        }

        [Test]
        public void TheTrialsPagesTwoLines_TheirNames_TheirWords_AndTheCueSteppingBothWays()
        {
            // The merge of 2026-11-26: the setting's two lines on work order 128's Trials page.
            Assert.That(TimingCueLines.Is(SettingLine.TimingCue) && TimingCueLines.Is(SettingLine.TimingClick), Is.True);
            Assert.That(TimingCueLines.Is(SettingLine.Attachments) || TimingCueLines.Is(SettingLine.CameraAngle), Is.False);
            Assert.That(TimingCueLines.Name(SettingLine.TimingCue), Is.EqualTo("Timing cue"));
            Assert.That(TimingCueLines.Name(SettingLine.TimingClick), Is.EqualTo("Timing click"));
            Assert.That(SettingsPages.PageOf(SettingLine.TimingCue), Is.EqualTo(SettingsPage.Trials));
            Assert.That(SettingsPages.PageOf(SettingLine.TimingClick), Is.EqualTo(SettingsPage.Trials));
            var forward = new List<string>();
            TimingCue c = TimingCue.BarOnly;
            for (int i = 0; i < TimingCues.Count; i++)
            {
                forward.Add(TimingCueLines.CueWords(c));
                c = TimingCueLines.Stepped(c, 1);
            }
            Assert.That(forward, Is.EqualTo(new[] { "Bar only", "Cylinder", "Ring", "Hammer" }), "the four names");
            Assert.That(c, Is.EqualTo(TimingCue.BarOnly), "round and back");
            Assert.That(TimingCueLines.Stepped(TimingCue.BarOnly, -1), Is.EqualTo(TimingCue.Hammer), "a step back");
            Assert.That(TimingCueLines.Stepped(TimingCue.Ring, -1), Is.EqualTo(TimingCue.Cylinder));
            Assert.That(TimingCueLines.ClickWords(true), Is.EqualTo("on"));
            Assert.That(TimingCueLines.ClickWords(false), Is.EqualTo("off"));
            Assert.That(TimingCueLines.CueWords(TimingCue.Ring), Is.SameAs(TimingCueLines.CueWords(TimingCue.Ring)), "the same string each time");
        }
    }
}
