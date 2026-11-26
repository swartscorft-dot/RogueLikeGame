using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 067 (his choice of 2026-11-21 for the evening, "Wide window og Blast"): the widening of a clean part or a
    /// perfect band at its edges (clamped at the window's start and end, given to the other side, at the cap, at 0); every
    /// reader of a part (the judge, the cast blade, the strip's box, the perfect band's judge and words) gives the same part;
    /// today's parts pinned with the stat at 0; Blast's one number for each of the twelve skills and the area upgrades, at each
    /// tier and at the cap, nothing else changed; both off give today's skills field for field; the two kinds' steps, caps,
    /// lines and cards; and the chest's offers over ten thousand seeded offers with twenty-four rows.
    /// </summary>
    public class WideWindowAndBlastTests
    {
        // Work order 142's second round moved two defaults (KitTimingChoices): these tests pin the numbers before it, so they
        // are given them here, and put back after.
        [SetUp]
        public void TheNumbersBefore142()
        {
            KitTimingChoices.LastPressAt036 = false;
            KitTimingChoices.BackstepFromHopsEnd = false;
        }

        [TearDown]
        public void PutBack142()
        {
            KitTimingChoices.LastPressAt036 = true;
            KitTimingChoices.BackstepFromHopsEnd = true;
        }

        static readonly string[] Four = { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" };
        const string A = OfferTexts.Arrow;
        const float Eps = 1e-5f;

        static void Say(string line)
        {
            try { TestContext.WriteLine(line); }
            catch (NullReferenceException) { Console.WriteLine(line); }
        }

        static SkillDefinition Skill(string name) => SkillDefaults.Create().Single(s => s.Name == name);
        static SkillUpgradeDefinition Row(string skill, string name) => SkillUpgradeTable.Find(SkillUpgradeTable.Create(), skill, name);
        static Upgrade Step(UpgradeKind kind, float amount, UpgradeTier tier = UpgradeTier.Common) =>
            new Upgrade { Kind = kind, Amount = amount, Tier = tier, Tiered = true };
        static string Fields(SkillDefinition s) =>
            string.Join(";", typeof(SkillDefinition).GetFields().Select(f => f.Name + "=" + Convert.ToString(f.GetValue(s), CultureInfo.InvariantCulture)));
        static SkillStatLayer Layer(float blast, float wide) => new SkillStatLayer(blast, wide, new StatValues().BlastSkills);

        // ---------------- The widening, at its edges ----------------

        // Work order 082 (review 6, finding 5): the three tests that read CleanPart.Of(kit, 0.6, false) pin Triple Tap's part with
        // the chain rhythm off (KitValues.ChainRhythm, on by default since work order 069): renamed to say so. With it on, the
        // rhythm's presses are WithTheChainRhythmOn_TripleTapsPresses_... below.
        [Test]
        public void ChainRhythmOff_TheStatAtZero_IsTodaysPartsExactly_Backstep003To023_TripleTap010To030_LongShot08To1()
        {
            var kit = new KitValues();
            SkillDefinition step = Skill("Backstep"), tap = Skill("Triple Tap"), shot = Skill("Long Shot");
            CleanPart hop = CleanPart.Of(kit, step.RecastWindow, true, 0f), taps = CleanPart.Of(kit, tap.RecastWindow, false, 0f);
            Assert.That(hop.From, Is.EqualTo(0.03f).Within(1e-6f));
            Assert.That(hop.To, Is.EqualTo(0.23f).Within(1e-6f));
            Assert.That(taps.From, Is.EqualTo(0.10f).Within(1e-6f));
            Assert.That(taps.To, Is.EqualTo(0.30f).Within(1e-6f));
            Assert.That(hop, Is.EqualTo(CleanPart.Of(kit, step.RecastWindow, true)), "exactly the part without the stat");
            Assert.That(taps, Is.EqualTo(CleanPart.Of(kit, tap.RecastWindow, false)));
            SkillDefinition layered = Layer(0f, 0f).Apply(shot);
            Assert.That(layered, Is.SameAs(shot), "no layer: the skill itself");
            Assert.That(shot.PerfectFrom, Is.EqualTo(0.8f));
            Assert.That(shot.PerfectTo, Is.EqualTo(1f));
            WideWindow.Widen(0.8f, 1f, 0f, 2.5f, 0f, out float f, out float t);
            Assert.That(f, Is.EqualTo(0.8f));
            Assert.That(t, Is.EqualTo(1f));
            WideWindow.Widen(0.8f, 1f, 0f, 2.5f, -0.2f, out f, out t);
            Assert.That(new[] { f, t }, Is.EqualTo(new[] { 0.8f, 1f }), "a share below 0 is none");
        }

        [Test]
        public void ThePartWidens_RoundItsMiddle_ByTheShareOfItsWidth()
        {
            var kit = new KitValues();
            CleanPart taps = CleanPart.Of(kit, 0.6f, false, 0.10f);
            Assert.That(taps.From, Is.EqualTo(0.09f).Within(Eps), "0.01 s earlier");
            Assert.That(taps.To, Is.EqualTo(0.31f).Within(Eps), "and 0.01 s later");
            Assert.That(taps.Width, Is.EqualTo(0.22f).Within(Eps));
            CleanPart at50 = CleanPart.Of(kit, 0.6f, false, 0.5f);
            Assert.That(at50.From, Is.EqualTo(0.05f).Within(Eps));
            Assert.That(at50.To, Is.EqualTo(0.35f).Within(Eps));
            SkillDefinition band = Layer(0f, 0.10f).Apply(Skill("Long Shot"));
            Assert.That(band.PerfectFrom, Is.EqualTo(0.79f).Within(Eps));
            Assert.That(band.PerfectTo, Is.EqualTo(1.01f).Within(Eps));
            Assert.That(band.BeforeStats.PerfectFrom, Is.EqualTo(0.8f), "the skill before the layer keeps its band");
        }

        [Test]
        public void ClampedAtTheWindowsStart_TheCutIsGivenToItsEnd_SoTheWidthGainedIsTheStats()
        {
            var kit = new KitValues();
            // Backstep's 0.03 to 0.23 s: +25 % is 0.005 to 0.255 (unclamped); +50 % would start at -0.02: 0 to 0.30.
            CleanPart at25 = CleanPart.Of(kit, 0.6f, true, 0.25f);
            Assert.That(at25.From, Is.EqualTo(0.005f).Within(Eps));
            Assert.That(at25.To, Is.EqualTo(0.255f).Within(Eps));
            CleanPart at50 = CleanPart.Of(kit, 0.6f, true, 0.5f);
            Assert.That(at50.From, Is.EqualTo(0f).Within(Eps), "never before the window opens");
            Assert.That(at50.To, Is.EqualTo(0.30f).Within(Eps), "the 0.02 s cut at the start is given to the end");
            Assert.That(at50.Width, Is.EqualTo(0.30f).Within(Eps), "0.20 s and its 50 %");
        }

        [Test]
        public void ClampedAtTheWindowsEnd_TheCutIsGivenToItsStart_AndAPartWiderThanTheWindow_IsTheWindow()
        {
            WideWindow.Widen(0.45f, 0.58f, 0f, 0.6f, 0.5f, out float f, out float t);
            Assert.That(t, Is.EqualTo(0.6f).Within(Eps), "never after the window ends");
            Assert.That(f, Is.EqualTo(0.405f).Within(Eps), "0.0125 s given to its start");
            Assert.That(t - f, Is.EqualTo(0.195f).Within(Eps), "the width gained is the stat's");
            WideWindow.Widen(0.1f, 0.5f, 0f, 0.6f, 1f, out f, out t);
            Assert.That(new[] { f, t }, Is.EqualTo(new[] { 0f, 0.6f }), "wider than the window: the whole window, no more");
            Assert.That(WideWindow.Widen(default(CleanPart), 0.6f, 0.5f).Any, Is.False, "no clean part: none to widen");
            Assert.That(WideWindow.Widen(new CleanPart(0.1f, 0.3f), 0.6f, 0f), Is.EqualTo(new CleanPart(0.1f, 0.3f)));
        }

        [Test]
        public void ChainRhythmOff_HerWideWindow_StopsAtItsCap_AndIsNothingWithTheSwitchOff()
        {
            var values = new RunValues();
            RunModifiers m = StatsDpsModel.Start(values);
            Assert.That(m.Stats.WideWindow, Is.Zero);
            for (int i = 0; i < 5; i++) m.Take(Step(UpgradeKind.WideWindow, 0.25f, UpgradeTier.Epic));
            Assert.That(m.Stats.WideWindow, Is.EqualTo(0.5f).Within(1e-6f), "+50 % at most");
            CleanPart capped = CleanPart.Of(new KitValues(), 0.6f, false, m.Stats.WideWindow);
            Assert.That(capped.Width, Is.EqualTo(0.30f).Within(Eps), "Triple Tap's 0.20 s at its cap");
            m.StatsAndTiers = false;
            Assert.That(m.Stats.WideWindow, Is.Zero, "off: nothing");
            Assert.That(m.Stats.Blast, Is.Zero);
            Assert.That(m.Stats.SkillLayer.Any, Is.False);
        }

        /// <summary>
        /// Work order 082 (review 6, finding 5): Triple Tap's presses as she plays them, the chain rhythm on (work order 069):
        /// the quick press clean at 0.12 to 0.30 s of its 0.45 s, the last at 0.42 to 0.66 s of its 0.80 s, and Wide window at
        /// its +50 % cap widening each round its middle inside its own window (the last 0.36 to 0.72 s); the cast blade and the
        /// strip's box read each press's part at the same shares.
        /// </summary>
        [TestCase(0f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        public void WithTheChainRhythmOn_TripleTapsPresses_AreTodaysParts_WideWindowWidensEach_AndTheReadersAgree(float wide)
        {
            var kit = new KitValues();
            Assert.That(kit.ChainRhythm, Is.True, "on by default since work order 069");
            SkillDefinition tap = Skill("Triple Tap");
            RecastBeat second = RecastRhythm.Press(kit, tap, 2, 0f), last = RecastRhythm.Press(kit, tap, 3, 0f);
            Assert.That(second.Window, Is.EqualTo(0.45f).Within(Eps), "the quick press's window");
            Assert.That(second.Clean.From, Is.EqualTo(0.12f).Within(Eps));
            Assert.That(second.Clean.To, Is.EqualTo(0.30f).Within(Eps));
            Assert.That(last.Window, Is.EqualTo(0.80f).Within(Eps), "the last press's window, a beat later");
            Assert.That(last.Clean.From, Is.EqualTo(0.42f).Within(Eps));
            Assert.That(last.Clean.To, Is.EqualTo(0.66f).Within(Eps));
            RecastBeat lastAtCap = RecastRhythm.Press(kit, tap, 3, 0.5f);
            Assert.That(lastAtCap.Clean.From, Is.EqualTo(0.36f).Within(Eps), "+50 %: 0.06 s earlier");
            Assert.That(lastAtCap.Clean.To, Is.EqualTo(0.72f).Within(Eps), "and 0.06 s later");

            var line = new RecastBeat[RecastRhythm.MostPresses];
            int n = RecastRhythm.Line(tap, kit, wide, line);
            Assert.That(n, Is.EqualTo(tap.RecastCount - 1), "a beat for each recast press");
            for (int i = 0; i < n; i++)
            {
                RecastBeat beat = line[i];
                Assert.That(beat.Clean, Is.EqualTo(WideWindow.Widen(RecastRhythm.Press(kit, tap, beat.Press, 0f).Clean, beat.Window, wide)), "press " + beat.Press + ": widened round its middle");
                for (float since = 0f; since <= beat.Window; since += 0.0025f)
                {
                    bool inPart = since >= beat.Clean.From && since <= beat.Clean.To;
                    bool edge = Math.Abs(since - beat.Clean.From) <= 1e-4f || Math.Abs(since - beat.Clean.To) <= 1e-4f;
                    CastBarView blade = CastBarRules.Recast(0.2f, beat.Window, 0.2f + since, since, beat.Clean);
                    if (!edge) Assert.That(blade.MarkerInZone, Is.EqualTo(inPart), "the blade, press " + beat.Press + " at " + since);
                    Assert.That(InterfaceRules.RecastShares(beat.Window, since, beat.Clean.From, beat.Clean.To, out float now, out float bandFrom, out float bandTo), Is.True);
                    Assert.That(bandFrom, Is.EqualTo(beat.Clean.From / beat.Window).Within(1e-6f), "the box, press " + beat.Press);
                    Assert.That(bandTo, Is.EqualTo(beat.Clean.To / beat.Window).Within(1e-6f));
                    if (!edge) Assert.That(now >= bandFrom && now <= bandTo, Is.EqualTo(inPart), "the box at " + since);
                }
            }
        }

        // ---------------- Every reader gives the same part ----------------

        // Work order 082 (review 6, finding 5): with the chain rhythm off (the part handed to each reader is CleanPart.Of's).
        [TestCase(0f)]
        [TestCase(0.10f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        public void ChainRhythmOff_EveryReaderOfTheCleanPart_GivesTheSamePart_TheJudgeTheBladeAndTheBox(float wide)
        {
            var kit = new KitValues();
            foreach (bool hop in new[] { true, false })
            {
                const float window = 0.6f;
                CleanPart part = CleanPart.Of(kit, window, hop, wide);
                Assert.That(part, Is.EqualTo(WideWindow.Widen(CleanPart.Of(kit, window, hop), window, wide)), "one function");
                // The judge, through a chain as the character runs it.
                for (float since = 0f; since <= window; since += 0.0025f)
                {
                    var chain = new RecastChain();
                    chain.Start(3, 2, window, hop);
                    chain.ShotWentOff(10f);
                    bool judged = chain.Judge(3, 10f + since, part, true) == RecastPress.Clean;
                    if (Math.Abs(since - part.From) > 1e-4f && Math.Abs(since - part.To) > 1e-4f)
                        Assert.That(judged, Is.EqualTo(since >= part.From && since <= part.To), "the judge at " + since + " (" + wide + ")");
                    Assert.That(chain.InCleanPart(3, 10f + since, part), Is.EqualTo(judged), "the rhythm reads the same part");
                    // The cast blade: the marker in the zone exactly when the judge says clean.
                    CastBarView blade = CastBarRules.Recast(0.2f, window, 0.2f + since, since, part);
                    if (Math.Abs(since - part.From) > 1e-4f && Math.Abs(since - part.To) > 1e-4f)
                        Assert.That(blade.MarkerInZone, Is.EqualTo(judged), "the blade at " + since);
                    // The strip's box: the band at the same shares.
                    Assert.That(InterfaceRules.RecastShares(window, since, part.From, part.To, out float now, out float bandFrom, out float bandTo), Is.True);
                    Assert.That(bandFrom, Is.EqualTo(part.From / window).Within(1e-6f));
                    Assert.That(bandTo, Is.EqualTo(part.To / window).Within(1e-6f));
                    if (Math.Abs(since - part.From) > 1e-4f && Math.Abs(since - part.To) > 1e-4f)
                        Assert.That(now >= bandFrom && now <= bandTo, Is.EqualTo(judged), "the box at " + since);
                }
            }
        }

        [TestCase(0f)]
        [TestCase(0.10f)]
        [TestCase(0.5f)]
        public void EveryReaderOfThePerfectBand_GivesTheSameBand_TheJudgeTheBladeAndTheWords(float wide)
        {
            foreach (SkillDefinition raw in new[] { Skill("Long Shot"), RunSkillUpgrades.Patched(Skill("Long Shot"), new[] { Row("Long Shot", "Overcharge") }) })
            {
                SkillDefinition shot = Layer(0f, wide).Apply(raw);
                WideWindow.Widen(raw.PerfectFrom, raw.PerfectTo, 0f, WideWindow.BandWindow(raw), wide, out float from, out float to);
                Assert.That(shot.PerfectFrom, Is.EqualTo(from));
                Assert.That(shot.PerfectTo, Is.EqualTo(to));
                Assert.That(to - from, Is.EqualTo((raw.PerfectTo - raw.PerfectFrom) * (1f + wide)).Within(Eps), "round its middle, the stat's width");
                Assert.That((from + to) * 0.5f, Is.EqualTo((raw.PerfectFrom + raw.PerfectTo) * 0.5f).Within(Eps), "its middle stays");
                CastBarRules.PerfectBand(shot, false, new FocusModeValues(), out float bladeFrom, out float bladeTo);
                Assert.That(bladeFrom, Is.EqualTo(from), "the cast blade's zone");
                Assert.That(bladeTo, Is.EqualTo(to));
                CastBarView view = CastBarRules.Charge(0f, shot.HoldMax, bladeFrom, bladeTo);
                for (float charged = 0.5f; charged <= 1.3f; charged += 0.0025f)
                {
                    if (Math.Abs(charged - from) < 2e-4f || Math.Abs(charged - to) < 2e-4f) continue;
                    bool perfect = PoolRules.IsPerfect(shot, charged);
                    Assert.That(perfect, Is.EqualTo(charged >= from && charged <= to), "the judge at " + charged);
                    view.Marker = Math.Min(1f, charged / view.Seconds);
                    Assert.That(view.MarkerInZone, Is.EqualTo(perfect), "the blade at " + charged);
                    TimingMiss miss = FeedbackRules.ChargeMiss(shot, charged, perfect, 0.5f);
                    Assert.That(miss, Is.EqualTo(perfect ? TimingMiss.None : charged < from ? TimingMiss.Early : TimingMiss.Late), "the words at " + charged);
                }
            }
        }

        // ---------------- Blast: the one number of each of the twelve ----------------

        /// <summary>The order's table: which number of each skill is its area, and nothing for the skills without one.</summary>
        static readonly Dictionary<string, string> s_AreaOf = new Dictionary<string, string>
        {
            { "Powder Keg", "Radius" }, { "Fan Fire", "Angle" }, { "Scatter Blast", "Angle" }, { "Heavy Volley", "HalfWidth" }, { "Breaker Slug", "HalfWidth" },
        };

        static float Field(SkillDefinition s, string field) => (float)typeof(SkillDefinition).GetField(field).GetValue(s);

        [Test]
        public void Blast_GrowsTheOneAreaNumberOfEachAreaSkill_AtEachTierAndTheCap_AndNothingElse()
        {
            var values = new RunValues();
            UpgradeDefinition row = values.TieredUpgrades.Single(r => r.Kind == UpgradeKind.Blast);
            float[] steps = UpgradeTiers.All.Select(t => TieredOffers.StepOf(row, t, values.Stats)).ToArray();
            Assert.That(steps, Is.EqualTo(new[] { 0.08f, 0.14f, 0.20f }), "+8 / 14 / 20 %");
            var shares = new List<float>(steps) { values.Stats.MostBlast };
            Say("skill, number: before  /  common  rare  epic  cap (+40 %)");
            foreach (SkillDefinition raw in SkillDefaults.Create())
            {
                bool area = s_AreaOf.TryGetValue(raw.Name, out string field);
                Assert.That(Blast.IsArea(raw, values.Stats.BlastSkills), Is.EqualTo(area), raw.Name);
                var grown = new List<string>();
                foreach (float share in shares)
                {
                    SkillDefinition after = Layer(share, 0f).Apply(raw);
                    if (!area)
                    {
                        Assert.That(after, Is.SameAs(raw), raw.Name + ": no area, untouched");
                        continue;
                    }
                    Assert.That(Field(after, field), Is.EqualTo(Field(raw, field) * (1f + share)).Within(1e-5f), raw.Name + " " + field);
                    Assert.That(Blast.Of(after), Is.EqualTo(Blast.Of(raw) * (1f + share)).Within(1e-5f));
                    Assert.That(after.Range, Is.EqualTo(raw.Range), raw.Name + ": never its reach");
                    // Nothing else changed: put the one number back and every field is the skill's.
                    typeof(SkillDefinition).GetField(field).SetValue(after, Field(raw, field));
                    Assert.That(Fields(after), Is.EqualTo(Fields(raw)), raw.Name + ": only its area");
                    grown.Add(Field(Layer(share, 0f).Apply(raw), field).ToString("0.###", CultureInfo.InvariantCulture));
                }
                Say(area ? raw.Name + ", " + field + ": " + Field(raw, field).ToString("0.###", CultureInfo.InvariantCulture) + "  /  " + string.Join("  ", grown)
                         : raw.Name + ": no area, untouched");
            }
            // The order's examples.
            Assert.That(Layer(0.08f, 0f).Apply(Skill("Powder Keg")).Radius, Is.EqualTo(2.7f).Within(1e-5f), "Powder Keg burst 2.5 m to 2.7 m");
            Assert.That(Layer(0.08f, 0f).Apply(Skill("Scatter Blast")).Angle, Is.EqualTo(75.6f).Within(1e-4f), "Scatter Blast cone 70 to 76 degrees");
            Assert.That(Layer(0.40f, 0f).Apply(Skill("Powder Keg")).Radius, Is.EqualTo(3.5f).Within(1e-5f), "at its cap");
        }

        [Test]
        public void Blast_AndTheAreaUpgrades_Multiply_BigKegThenBlast_SlugsLineGrowsItsWidth()
        {
            SkillStatLayer layer = Layer(0.08f, 0f);
            SkillDefinition big = RunSkillUpgrades.Patched(Skill("Powder Keg"), new[] { Row("Powder Keg", "Big Keg") }, layer);
            Assert.That(big.BeforeStats.Radius, Is.EqualTo(2.5f * 4f / 3f).Within(1e-5f), "Big Keg: 2.5 m to 3.33 m");
            Assert.That(big.Radius, Is.EqualTo(2.5f * 4f / 3f * 1.08f).Within(1e-5f), "then Blast: 3.6 m");
            Say("Big Keg 2.5 m -> " + big.BeforeStats.Radius.ToString("0.00", CultureInfo.InvariantCulture) + " m, then Blast +8 % -> " + big.Radius.ToString("0.00", CultureInfo.InvariantCulture) + " m");
            Assert.That(RunSkillUpgrades.Patched(Skill("Powder Keg"), new[] { Row("Powder Keg", "Big Keg") }, Layer(0.4f, 0f)).Radius,
                        Is.EqualTo(2.5f * 4f / 3f * 1.4f).Within(1e-5f), "4.67 m at the cap");
            SkillDefinition slug = RunSkillUpgrades.Patched(Skill("Scatter Blast"), new[] { Row("Scatter Blast", "Slug") }, layer);
            Assert.That(slug.Shape, Is.EqualTo(SkillShape.Line), "Slug's line in place of a cone");
            Assert.That(slug.HalfWidth, Is.EqualTo(0.4f * 1.08f).Within(1e-5f), "the line's width grows, not the cone's angle it no longer uses");
            Assert.That(slug.Angle, Is.EqualTo(70f), "the angle it no longer uses stays");
            Assert.That(slug.Range, Is.EqualTo(10f), "never its reach");
            // Every elite upgrade of the table, with Blast at its cap: an area skill's upgrade first, then the layer.
            foreach (SkillUpgradeDefinition u in SkillUpgradeTable.Create())
            {
                SkillDefinition before = RunSkillUpgrades.Patched(Skill(u.Skill), new[] { u });
                SkillDefinition after = RunSkillUpgrades.Patched(Skill(u.Skill), new[] { u }, Layer(0.4f, 0.5f));
                if (!Blast.IsArea(before, new StatValues().BlastSkills) && !WideWindow.HasBand(before))
                    Assert.That(after, Is.SameAs(after.BeforeStats), u.FullName + ": nothing to lay on");
                else Assert.That(Fields(after.BeforeStats), Is.EqualTo(Fields(before)), u.FullName + ": the layer after the upgrade");
            }
        }

        [Test]
        public void BothOff_EverySkillAndEveryAreaUpgrade_IsTodaysFieldForField()
        {
            var values = new RunValues { StatsAndTiers = false };
            RunModifiers m = StatsDpsModel.Start(values);
            m.StatsAndTiers = false;
            m.Take(Step(UpgradeKind.Blast, 0.2f, UpgradeTier.Epic));
            m.Take(Step(UpgradeKind.WideWindow, 0.25f, UpgradeTier.Epic));
            SkillStatLayer off = m.Stats.SkillLayer;
            Assert.That(off.Any, Is.False, "with the switch off the steps do nothing");
            SkillDefinition[] settings = SkillDefaults.Create();
            var run = new RunSkillUpgrades();
            Assert.That(run.View(settings, off), Is.SameAs(settings), "no upgrade and no layer: the settings' own list");
            Assert.That(run.View(settings, Layer(0f, 0f)), Is.SameAs(settings), "a layer of 0: the same");
            foreach (SkillUpgradeDefinition u in SkillUpgradeTable.Create())
            {
                SkillDefinition raw = settings.Single(s => s.Name == u.Skill);
                Assert.That(Fields(RunSkillUpgrades.Patched(raw, new[] { u }, off)), Is.EqualTo(Fields(RunSkillUpgrades.Patched(raw, new[] { u }))), u.FullName);
            }
            run.Take(Row("Powder Keg", "Big Keg"));
            IReadOnlyList<SkillDefinition> view = run.View(settings, off);
            // Work order 082 (review 6, finding 24): the lines above hold by construction (a layer that is off is the same
            // object); the numbers themselves pinned: the settings' keg 2.5 m, Big Keg's a third wider, 3.333 m, with both off.
            Assert.That(settings.Single(s => s.Name == "Powder Keg").Radius, Is.EqualTo(2.5f).Within(1e-4f), "the keg as written");
            Assert.That(view.Single(s => s.Name == "Powder Keg").Radius, Is.EqualTo(3.3333f).Within(1e-3f), "Big Keg, a third wider, and no Blast with the switch off");
            for (int i = 0; i < settings.Length; i++)
                Assert.That(Fields(view[i]), Is.EqualTo(Fields(run.View(settings)[i])), settings[i].Name + ": as the view without a layer");
            // The stats on and none taken: still the settings' own.
            m.StatsAndTiers = true;
            m.TakeBack(m.Taken.Last());
            m.TakeBack(m.Taken.Last());
            Assert.That(m.Stats.SkillLayer.Any, Is.False);
            Assert.That(new RunSkillUpgrades().View(settings, m.Stats.SkillLayer), Is.SameAs(settings));
        }

        [Test]
        public void TheView_IsMadeOnceForALayer_AndAgainWhenTheLayerChanges()
        {
            SkillDefinition[] settings = SkillDefaults.Create();
            var run = new RunSkillUpgrades();
            var stats = new StatValues();
            IReadOnlyList<SkillDefinition> a = run.View(settings, new SkillStatLayer(0.08f, 0f, stats.BlastSkills));
            Assert.That(a, Is.Not.SameAs(settings));
            Assert.That(run.View(settings, new SkillStatLayer(0.08f, 0f, stats.BlastSkills)), Is.SameAs(a), "the same layer: the same view");
            IReadOnlyList<SkillDefinition> b = run.View(settings, new SkillStatLayer(0.16f, 0f, stats.BlastSkills));
            Assert.That(b, Is.Not.SameAs(a), "a step more: made again");
            Assert.That(b.Single(s => s.Name == "Powder Keg").Radius, Is.EqualTo(2.5f * 1.16f).Within(1e-5f));
            Assert.That(settings.Single(s => s.Name == "Powder Keg").Radius, Is.EqualTo(2.5f), "nothing written to the settings");
        }

        // ---------------- The two kinds in the chest ----------------

        [Test]
        public void TheTwoKinds_TheirSteps_TheirCaps_NeverOfferedAtTheCap_AndTheirWords()
        {
            var values = new RunValues();
            UpgradeDefinition R(UpgradeKind k) => values.TieredUpgrades.Single(r => r.Kind == k);
            float[] Steps(UpgradeKind k) => UpgradeTiers.All.Select(t => TieredOffers.StepOf(R(k), t, values.Stats)).ToArray();
            Assert.That(values.TieredUpgrades.Length, Is.EqualTo(24), "twenty-four rows");
            Assert.That(Steps(UpgradeKind.WideWindow), Is.EqualTo(new[] { 0.10f, 0.18f, 0.25f }));
            Assert.That(Steps(UpgradeKind.Blast), Is.EqualTo(new[] { 0.08f, 0.14f, 0.20f }));
            Assert.That(values.Stats.MostWideWindow, Is.EqualTo(0.5f));
            Assert.That(values.Stats.MostBlast, Is.EqualTo(0.4f));
            Assert.That(TieredOffers.StatOf(UpgradeKind.WideWindow), Is.EqualTo("wide window"));
            Assert.That(TieredOffers.StatOf(UpgradeKind.Blast), Is.EqualTo("blast"));
            Assert.That(values.TieredUpgrades.Select(r => TieredOffers.StatOf(r.Kind)).Count(s => s == "wide window" || s == "blast"), Is.EqualTo(2), "a stat of its own each");

            RunModifiers m = StatsDpsModel.Start(values);
            Assert.That(TieredOffers.Useful(R(UpgradeKind.WideWindow), Four, values, values.Stats, m, false), Is.True);
            Assert.That(TieredOffers.Useful(R(UpgradeKind.Blast), Four, values, values.Stats, m, false), Is.True);
            m.Take(Step(UpgradeKind.WideWindow, 0.25f));
            m.Take(Step(UpgradeKind.Blast, 0.2f));
            Assert.That(m.Stats.WideWindow, Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(m.Stats.Blast, Is.EqualTo(0.2f).Within(1e-6f));
            m.Take(Step(UpgradeKind.WideWindow, 0.25f));
            m.Take(Step(UpgradeKind.Blast, 0.2f));
            Assert.That(TieredOffers.Useful(R(UpgradeKind.WideWindow), Four, values, values.Stats, m, false), Is.False, "at its cap: never offered");
            Assert.That(TieredOffers.Useful(R(UpgradeKind.Blast), Four, values, values.Stats, m, false), Is.False);
            m.Take(Step(UpgradeKind.Blast, 0.2f));
            Assert.That(m.Stats.Blast, Is.EqualTo(0.4f).Within(1e-6f), "+40 % at most");

            List<string> lines = m.Stats.Lines(0.225f, 10f, 100);
            Assert.That(lines.Count, Is.EqualTo(18), "065's sixteen and these two");
            Assert.That(lines[16], Is.EqualTo("Wide window  +50 %  (+0 %)"), "after 065's");
            Assert.That(lines[17], Is.EqualTo("Blast  +40 %  (+0 %)"));
            StringAssert.EndsWith(", wide window +50%, blast +40%", m.Stats.OneLine(0.225f, 100));
            Assert.That(Step(UpgradeKind.WideWindow, 0.1f).Short, Is.EqualTo("+10% window"));
            Assert.That(Step(UpgradeKind.Blast, 0.08f).Short, Is.EqualTo("+8% area"));
            StringAssert.Contains("wider clean parts", Step(UpgradeKind.WideWindow, 0.1f).Text);
            StringAssert.Contains("area skills", Step(UpgradeKind.Blast, 0.08f).Text);
        }

        // ---------------- Their cards ----------------

        static OfferContext Her(params string[] skills)
        {
            SkillDefinition[] all = SkillDefaults.Create();
            return new OfferContext
            {
                Modifiers = StatsDpsModel.Start(new RunValues()), Health = 72, OwnMaxHealth = 100,
                Equipped = all.Where(s => skills.Contains(s.Name)).ToList(),
            };
        }

        /// <summary>Her skills as the run's view would make them with her stats now: what the game hands the cards.</summary>
        static void AsSheHasThem(OfferContext c)
        {
            SkillStatLayer layer = c.Stats.SkillLayer;
            c.Equipped = c.Equipped.Select(s => layer.Apply(s.BeforeStats)).ToList();
        }

        [Test]
        public void TheWideWindowCard_ReadsAsOrdered_ForTheSkillsSheHas()
        {
            OfferCard card = OfferTexts.ForUpgrade(Step(UpgradeKind.WideWindow, 0.10f), Her(Four));
            Say(card.ToString().Replace("\n", "  |  "));
            Assert.That(card.Name, Is.EqualTo("Wide window"));
            Assert.That(card.TierLine, Is.EqualTo("COMMON"));
            Assert.That(card.Sentence, Is.EqualTo("The clean part of a window and the perfect band are wider."));
            Assert.That(card.Numbers, Is.EqualTo(new[] { "Backstep clean part  0.20 s" + A + "0.22 s", "Triple Tap clean part  0.24 s" + A + "0.26 s" }), "Triple Tap's last press, a beat later and 0.24 s wide since work order 069 (0.20 s before)");
            OfferCard withShot = OfferTexts.ForUpgrade(Step(UpgradeKind.WideWindow, 0.10f), Her("Long Shot", "Quick Shot"));
            Assert.That(withShot.Numbers, Is.EqualTo(new[] { "Long Shot perfect band  0.20 s" + A + "0.22 s" }));
            OfferCard none = OfferTexts.ForUpgrade(Step(UpgradeKind.WideWindow, 0.10f), Her("Quick Shot", "Powder Keg"));
            Assert.That(none.Numbers, Is.EqualTo(new[] { "clean parts and perfect bands  +0 %" + A + "+10 %" }), "with no such skill: the stat");
            OfferCard all = OfferTexts.ForUpgrade(Step(UpgradeKind.WideWindow, 0.25f, UpgradeTier.Epic), Her("Backstep", "Triple Tap", "Long Shot", "Scatter Blast"));
            Assert.That(all.Numbers.Count, Is.EqualTo(3), "at most three lines");
            Assert.That(all.Numbers[0], Is.EqualTo("Long Shot perfect band  0.20 s" + A + "0.25 s"), "in the order of her list");
            Assert.That(all.Numbers[1], Is.EqualTo("Backstep clean part  0.20 s" + A + "0.25 s"));
        }

        [Test]
        public void TheWideWindowCardsAfter_IsWhatTheJudgeReads_WithTheCardTaken()
        {
            OfferContext c = Her("Backstep", "Long Shot");
            c.Modifiers.Take(Step(UpgradeKind.WideWindow, 0.18f, UpgradeTier.Rare));
            AsSheHasThem(c);
            Upgrade u = Step(UpgradeKind.WideWindow, 0.25f, UpgradeTier.Epic);
            int before = c.Modifiers.Count;
            OfferCard card = OfferTexts.ForUpgrade(u, c);
            Assert.That(c.Modifiers.Count, Is.EqualTo(before), "the card took its upgrade back");
            c.Modifiers.Take(u);
            float step = CleanPart.Of(c.Kit, 0.6f, true, c.Stats.WideWindow).Width;
            SkillDefinition shot = c.Stats.SkillLayer.Apply(Skill("Long Shot"));
            Assert.That(card.Numbers[1], Is.EqualTo("Backstep clean part  0.24 s" + A + step.ToString("0.00", CultureInfo.InvariantCulture) + " s"));
            Assert.That(card.Numbers[0], Does.EndWith(A + (shot.PerfectTo - shot.PerfectFrom).ToString("0.00", CultureInfo.InvariantCulture) + " s"), "the band the judge reads");
            Say(card.ToString().Replace("\n", "  |  "));
        }

        [Test]
        public void TheBlastCard_ReadsAsOrdered_AndItsAfterIsTheSkillWithTheCardTaken()
        {
            OfferCard card = OfferTexts.ForUpgrade(Step(UpgradeKind.Blast, 0.08f), Her(Four));
            Say(card.ToString().Replace("\n", "  |  "));
            Assert.That(card.Name, Is.EqualTo("Blast"));
            Assert.That(card.Sentence, Is.EqualTo("Your area skills hit a wider area."));
            Assert.That(card.Numbers, Is.EqualTo(new[] { "Scatter Blast cone  70°" + A + "76°" }), "the one area skill of the start four");
            OfferCard more = OfferTexts.ForUpgrade(Step(UpgradeKind.Blast, 0.08f), Her("Powder Keg", "Scatter Blast", "Heavy Volley", "Breaker Slug", "Quick Shot"));
            Say(more.ToString().Replace("\n", "  |  "));
            Assert.That(more.Numbers, Is.EqualTo(new[] { "Powder Keg burst  2.5 m" + A + "2.7 m", "Scatter Blast cone  70°" + A + "76°", "Heavy Volley width  2 m" + A + "2.2 m" }), "at most three");
            Assert.That(OfferTexts.ForUpgrade(Step(UpgradeKind.Blast, 0.08f), Her("Quick Shot", "Backstep")).Numbers,
                        Is.EqualTo(new[] { "area skills  +0 %" + A + "+8 %" }), "with none owned: the stat");
            // Its after: take the card, read the skill as the view makes it.
            OfferContext c = Her("Powder Keg");
            c.Modifiers.Take(Step(UpgradeKind.Blast, 0.14f, UpgradeTier.Rare));
            AsSheHasThem(c);
            Upgrade u = Step(UpgradeKind.Blast, 0.2f, UpgradeTier.Epic);
            string line = OfferTexts.ForUpgrade(u, c).Numbers.Single();
            float was = c.Skill("Powder Keg").Radius;
            c.Modifiers.Take(u);
            float radius = new RunSkillUpgrades().View(SkillDefaults.Create(), c.Stats.SkillLayer).Single(s => s.Name == "Powder Keg").Radius;
            Assert.That(radius, Is.EqualTo(2.5f * 1.34f).Within(1e-5f), "2.5 m, +14 % and +20 %");
            Assert.That(line, Is.EqualTo("Powder Keg burst  " + OfferTexts.Metres(was) + A + OfferTexts.Metres(radius)), "2.5 m +14 %, then +34 %: " + radius);
        }

        [Test]
        public void AnElitesAreaUpgradeCard_WithBlastTaken_SaysTheSkillAsSheWillHaveIt()
        {
            OfferContext c = Her("Powder Keg", "Scatter Blast");
            c.Modifiers.Take(Step(UpgradeKind.Blast, 0.2f, UpgradeTier.Epic));
            AsSheHasThem(c);
            SkillDefinition keg = c.Skill("Powder Keg"), blast = c.Skill("Scatter Blast");
            Assert.That(keg.Radius, Is.EqualTo(3f).Within(1e-5f), "as she has it: 2.5 m +20 %");
            OfferCard big = OfferTexts.ForSkillUpgrade(Row("Powder Keg", "Big Keg"), keg, "5", c);
            Say(big.ToString().Replace("\n", "  |  "));
            Assert.That(big.Numbers, Does.Contain("burst radius  3 m" + A + "4 m"), "Big Keg then Blast: 2.5 x 4/3 x 1.2");
            OfferCard slug = OfferTexts.ForSkillUpgrade(Row("Scatter Blast", "Slug"), blast, "2", c);
            Say(slug.ToString().Replace("\n", "  |  "));
            Assert.That(slug.Numbers, Does.Contain("area  a cone 84°" + A + "a line 1 m wide"), "Slug's 0.8 m line, then +20 %: 0.96 m");
        }

        // ---------------- The offers ----------------

        [Test]
        public void TenThousandSeededOffers_WithTwentyFourRows_HowOftenEachKindComesUp()
        {
            var values = new RunValues();
            Dictionary<UpgradeKind, int> now = Offers(values, 1000, out int offers);
            Assert.That(offers, Is.EqualTo(10000));
            var before065 = new RunValues { TieredUpgradesOffered = 2 };
            before065.TieredUpgrades = before065.TieredUpgrades.Where(r => r.Kind <= UpgradeKind.Luck).ToArray();
            Dictionary<UpgradeKind, int> then = Offers(before065, 1000, out int offersThen);
            var before067 = new RunValues();
            before067.TieredUpgrades = before067.TieredUpgrades.Where(r => r.Kind <= UpgradeKind.SecondDash).ToArray();
            Dictionary<UpgradeKind, int> mid = Offers(before067, 1000, out int offersMid);
            Say("Kind: share of 10 000 offers that show it, now (three a chest, 24 rows)  /  before this order (three, 22 rows)  /  before 065 (two, 16 rows)");
            foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind)).Cast<UpgradeKind>())
            {
                double Share(Dictionary<UpgradeKind, int> d, int n) => d.TryGetValue(kind, out int x) ? x / (double)n : 0.0;
                Say(string.Format(CultureInfo.InvariantCulture, "  {0,-14} {1,6:0.0} %   {2,6:0.0} %   {3,6:0.0} %", kind, Share(now, offers) * 100.0, Share(mid, offersMid) * 100.0, Share(then, offersThen) * 100.0));
            }
            foreach (UpgradeKind kind in new[] { UpgradeKind.AttackSpeed, UpgradeKind.FlatDamage, UpgradeKind.CritChance, UpgradeKind.CritDamage })
                Assert.That(now[kind] / (double)offers, Is.GreaterThan(0.5 * then[kind] / offersThen), kind + " offered at least half as often as before 065");
            foreach (UpgradeKind kind in new[] { UpgradeKind.WideWindow, UpgradeKind.Blast })
                Assert.That(now[kind] / (double)offers, Is.InRange(0.05, 0.3), kind + ": comes up");
        }

        /// <summary>Runs of five floors, two chests a floor, each chest's first card taken, her hurt in one run of two (065's measurement).</summary>
        static Dictionary<UpgradeKind, int> Offers(RunValues values, int runs, out int offers)
        {
            var seen = new Dictionary<UpgradeKind, int>();
            offers = 0;
            for (int run = 1; run <= runs; run++)
            {
                RunModifiers m = StatsDpsModel.Start(values);
                ulong seed = (ulong)run * 104729UL;
                for (int k = 0; k < 10; k++)
                {
                    List<Upgrade> offer = ChestUpgrades.Offer(seed, 1 + k / 2, 2 + 3 * (k % 2), Four, values, m, run % 2 == 0);
                    offers++;
                    foreach (UpgradeKind kind in offer.Select(u => u.Kind).Distinct()) seen[kind] = seen.TryGetValue(kind, out int n) ? n + 1 : 1;
                    if (offer.Count > 0) m.Take(offer[0]);
                }
            }
            return seen;
        }
    }
}
