using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 021 (2026-11-20), the placeholder sounds as rules: every recipe gives samples of its length that start
    /// and end in silence, stay under full scale, hold no NaN, are not all zero, and are the same when made twice; the
    /// crowd rule (the cap per second, the merge within a frame, the louder merge capped); the pitch variation stays in
    /// its range and repeats for a seed; which moment gives which sound; what may start while paused; the volumes.
    /// Nobody can hear here whether they sound right.
    /// </summary>
    public class PlaceholderSoundRulesTests
    {
        static readonly PlaceholderSoundValues V = new PlaceholderSoundValues();

        /// <summary>The lengths in the work order's table.</summary>
        static readonly Dictionary<PlaceholderSound, float> TableSeconds = new Dictionary<PlaceholderSound, float>
        {
            { PlaceholderSound.EnemyHit, 0.05f }, { PlaceholderSound.EnemyDies, 0.25f }, { PlaceholderSound.WindupRed, 0.25f },
            { PlaceholderSound.WindupBlue, 0.25f }, { PlaceholderSound.Strike, 0.2f }, { PlaceholderSound.Arrow, 0.15f },
            { PlaceholderSound.Hurt, 0.15f }, { PlaceholderSound.HerDeath, 0.8f }, { PlaceholderSound.Dash, 0.2f },
            { PlaceholderSound.Dazed, 0.3f }, { PlaceholderSound.BossCharge, 0.6f }, { PlaceholderSound.BossAnger, 0.8f },
            { PlaceholderSound.BossCall, 0.7f }, { PlaceholderSound.ChestOpens, 0.4f }, { PlaceholderSound.ChoiceTaken, 0.3f },
            { PlaceholderSound.FloorCleared, 0.6f }, { PlaceholderSound.ActCleared, 1.2f }, { PlaceholderSound.Menu, 0.03f },
            { PlaceholderSound.FocusFull, 0.3f },
            // Changed deliberately (work order 048): the ten sounds for the new things, at the lengths that order gave them.
            { PlaceholderSound.ProjectileStopped, 0.08f }, { PlaceholderSound.OrbLoosed, 0.18f }, { PlaceholderSound.Duck, 0.12f },
            { PlaceholderSound.Stunned, 0.45f }, { PlaceholderSound.Pulled, 0.18f }, { PlaceholderSound.DropLands, 0.35f },
            { PlaceholderSound.DropTaken, 0.2f }, { PlaceholderSound.WayOpens, 0.6f }, { PlaceholderSound.Crit, 0.06f },
            { PlaceholderSound.RareOffer, 0.5f },
        };

        static IEnumerable<PlaceholderSound> Sounds() => PlaceholderSoundRules.All;

        // ---------------- The synthesiser and the recipes ----------------

        [TestCaseSource(nameof(Sounds))]
        public void EachRecipe_IsItsLength_StartsAndEndsInSilence_UnderFullScale_NoNaN_NotSilent(PlaceholderSound sound)
        {
            float[] s = Synth.Render(PlaceholderRecipes.For(sound), Synth.SampleRate);
            Assert.That(TableSeconds.ContainsKey(sound), Is.True, "in the table");
            Assert.That(PlaceholderRecipes.Seconds(sound), Is.EqualTo(TableSeconds[sound]).Within(1e-6f), "the table's length");
            Assert.That(s.Length, Is.EqualTo((int)Math.Round(TableSeconds[sound] * Synth.SampleRate)), "samples of its length");
            Assert.That(s.Any(v => float.IsNaN(v) || float.IsInfinity(v)), Is.False, "no NaN");
            float peak = s.Max(v => Math.Abs(v));
            Assert.That(peak, Is.LessThan(1f), "under full scale");
            Assert.That(peak, Is.LessThanOrEqualTo(0.9f + 1e-4f), "no higher than its recipe's peak");
            Assert.That(peak, Is.GreaterThan(0.3f), "not all zero");
            Assert.That(s[0], Is.EqualTo(0f), "from silence");
            Assert.That(s[s.Length - 1], Is.EqualTo(0f), "to silence");
            int edge = (int)(0.0005f * Synth.SampleRate);
            Assert.That(s.Take(edge).Max(v => Math.Abs(v)), Is.LessThan(0.25f), "no click at the start");
            Assert.That(s.Skip(s.Length - edge).Max(v => Math.Abs(v)), Is.LessThan(0.25f), "no click at the end");
        }

        [TestCaseSource(nameof(Sounds))]
        public void EachRecipe_IsTheSame_WhenMadeTwice(PlaceholderSound sound)
        {
            float[] a = Synth.Render(PlaceholderRecipes.For(sound), Synth.SampleRate);
            float[] b = Synth.Render(PlaceholderRecipes.For(sound), Synth.SampleRate);
            Assert.That(b, Is.EqualTo(a));
        }

        [Test]
        public void TheSounds_AreAllDifferent()
        {
            var made = PlaceholderSoundRules.All.Select(s => Synth.Render(PlaceholderRecipes.For(s))).ToList();
            for (int i = 0; i < made.Count; i++)
                for (int j = i + 1; j < made.Count; j++)
                    Assert.That(made[i].Length == made[j].Length && made[i].SequenceEqual(made[j]), Is.False,
                                PlaceholderSoundRules.All[i] + " and " + PlaceholderSoundRules.All[j]);
        }

        [Test]
        public void ARedAndABlueWindup_AreDifferent_AndTheBlueIsHigher()
        {
            float[] red = Synth.Render(PlaceholderRecipes.For(PlaceholderSound.WindupRed));
            float[] blue = Synth.Render(PlaceholderRecipes.For(PlaceholderSound.WindupBlue));
            Assert.That(blue, Is.Not.EqualTo(red));
            int n = Math.Min(red.Length, blue.Length);
            Assert.That(Crossings(blue, 0, n), Is.GreaterThan(3 * Crossings(red, 0, n)), "the blue one is much higher");
        }

        [Test]
        public void ASineSlide_GoesFromItsPitchToItsOther()
        {
            var recipe = new SynthRecipe
            {
                Seconds = 1f, Peak = 0.9f,
                Layers = new[] { new SynthLayer { Wave = SynthWave.Sine, From = 200f, To = 800f, Length = 1f, Attack = 0.001f, Release = 0.001f } },
            };
            float[] s = Synth.Render(recipe);
            int tenth = Synth.SampleRate / 10;
            float first = Crossings(s, 0, tenth) / 2f / 0.1f, last = Crossings(s, s.Length - tenth, s.Length) / 2f / 0.1f;
            Assert.That(first, Is.EqualTo(200f * Math.Pow(4, 0.05)).Within(25f), "near 200 Hz at the start");
            Assert.That(last, Is.EqualTo(800f * Math.Pow(4, -0.05)).Within(60f), "near 800 Hz at the end");
        }

        [Test]
        public void TheLowPass_TakesTheHighsOffNoise()
        {
            SynthRecipe Noise(float low) => new SynthRecipe
            {
                Seconds = 0.5f, Peak = 0.9f,
                Layers = new[] { new SynthLayer { Wave = SynthWave.Noise, Length = 0.5f, LowPass = low } },
            };
            float[] open = Synth.Render(Noise(0f)), dull = Synth.Render(Noise(200f));
            Assert.That(Crossings(dull, 0, dull.Length), Is.LessThan(Crossings(open, 0, open.Length) / 4), "fewer crossings: duller");
        }

        [Test]
        public void ARecipeWithNoLayers_OrNoLength_IsSilence_AndNoNaN()
        {
            float[] none = Synth.Render(new SynthRecipe { Seconds = 0.1f, Layers = new SynthLayer[0] });
            Assert.That(none.All(v => v == 0f), Is.True);
            float[] broken = Synth.Render(new SynthRecipe
            {
                Seconds = 0.1f, Drive = float.NaN, Peak = 3f,
                Layers = new[] { new SynthLayer { From = float.NaN, To = -5f, Gain = 1f, Length = 0.1f, Decay = float.NaN, LowPass = float.PositiveInfinity } },
            });
            Assert.That(broken.Any(float.IsNaN), Is.False);
            Assert.That(broken.Max(v => Math.Abs(v)), Is.LessThan(1f), "a peak asked over full scale is held under it");
            Assert.That(Synth.Render(null).All(v => v == 0f), Is.True);
        }

        static int Crossings(float[] s, int from, int to)
        {
            int n = 0;
            for (int i = Math.Max(1, from); i < Math.Min(to, s.Length); i++)
                if ((s[i - 1] < 0f) != (s[i] < 0f)) n++;
            return n;
        }

        // ---------------- The crowd rule ----------------

        [Test]
        public void TheHitTick_PlaysAtMostTwelveTimesASecond_AskedEveryFrame()
        {
            var crowd = new SoundCrowd();
            int plays = 0;
            for (int frame = 0; frame < 120; frame++) // two seconds at 60 frames a second
            {
                crowd.Ask(PlaceholderSound.EnemyHit);
                if (crowd.Flush(frame / 60f, V).Count > 0) plays++;
                if (frame == 59) Assert.That(plays, Is.EqualTo(12), "twelve in the first second");
            }
            Assert.That(V.PerSecond(PlaceholderSound.EnemyHit), Is.EqualTo(12));
            Assert.That(plays, Is.EqualTo(24), "and twelve in the next");
        }

        [TestCase(PlaceholderSound.EnemyHit, 12)]
        [TestCase(PlaceholderSound.WindupRed, 4)]
        [TestCase(PlaceholderSound.WindupBlue, 4)]
        [TestCase(PlaceholderSound.EnemyDies, 6)]
        public void TheCapPerSecond_IsTheFirstGuess(PlaceholderSound sound, int cap)
        {
            Assert.That(V.PerSecond(sound), Is.EqualTo(cap));
            var crowd = new SoundCrowd();
            int plays = 0;
            for (int i = 0; i < 100; i++)
            {
                crowd.Ask(sound);
                plays += crowd.Flush(i * 0.0003f, V).Count; // frames without a window: 0.3 ms
            }
            Assert.That(plays, Is.EqualTo(cap), "a burst of asks within a second plays the cap and no more");
            crowd.Ask(sound);
            Assert.That(crowd.Flush(1.05f, V).Count, Is.EqualTo(1), "a second later it plays again");
        }

        [Test]
        public void TwentyOfOneSound_InOneFrame_AreOnePlay_ALittleLouder_ButCapped()
        {
            var crowd = new SoundCrowd();
            for (int i = 0; i < 20; i++) crowd.Ask(PlaceholderSound.EnemyHit);
            IReadOnlyList<SoundPlay> plays = crowd.Flush(0f, V);
            Assert.That(plays.Count, Is.EqualTo(1), "one play");
            Assert.That(plays[0].Asked, Is.EqualTo(20));
            Assert.That(plays[0].Gain, Is.EqualTo(V.MergeMost).Within(1e-5f), "louder, but no more than the cap");

            for (int i = 0; i < 3; i++) crowd.Ask(PlaceholderSound.Strike);
            plays = crowd.Flush(0.1f, V);
            Assert.That(plays.Single().Gain, Is.EqualTo(1f + 2f * V.MergeStep).Within(1e-5f), "three: two steps louder");

            crowd.Ask(PlaceholderSound.Strike);
            Assert.That(crowd.Flush(0.2f, V).Single().Gain, Is.EqualTo(1f), "one alone: as it is");
            Assert.That(crowd.Flush(0.3f, V), Is.Empty, "nothing asked, nothing played");
        }

        [Test]
        public void Merged_IsOneForOne_RisesByItsStep_AndStopsAtItsCap()
        {
            Assert.That(PlaceholderSoundRules.Merged(0, 0.15f, 1.5f), Is.EqualTo(1f));
            Assert.That(PlaceholderSoundRules.Merged(1, 0.15f, 1.5f), Is.EqualTo(1f));
            Assert.That(PlaceholderSoundRules.Merged(2, 0.15f, 1.5f), Is.EqualTo(1.15f).Within(1e-5f));
            Assert.That(PlaceholderSoundRules.Merged(4, 0.15f, 1.5f), Is.EqualTo(1.45f).Within(1e-5f));
            Assert.That(PlaceholderSoundRules.Merged(5, 0.15f, 1.5f), Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(PlaceholderSoundRules.Merged(24, 0.15f, 1.5f), Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void DifferentSounds_InOneFrame_AreEachPlayed_AndAMergeKeepsTheLoudestAsk()
        {
            var crowd = new SoundCrowd();
            crowd.Ask(PlaceholderSound.EnemyHit);
            crowd.Ask(PlaceholderSound.EnemyDies);
            crowd.Ask(PlaceholderSound.Hurt, 0.7f);
            crowd.Ask(PlaceholderSound.Hurt, 0.9f);
            IReadOnlyList<SoundPlay> plays = crowd.Flush(0f, V);
            Assert.That(plays.Select(p => p.Sound), Is.EquivalentTo(new[] { PlaceholderSound.EnemyHit, PlaceholderSound.EnemyDies, PlaceholderSound.Hurt }));
            Assert.That(plays.Single(p => p.Sound == PlaceholderSound.Hurt).Gain, Is.EqualTo(0.9f * (1f + V.MergeStep)).Within(1e-5f));
        }

        [Test]
        public void ADroppedAsk_DoesNotCountTowardTheCap_AndClearForgetsAll()
        {
            var crowd = new SoundCrowd();
            for (int i = 0; i < 6; i++)
            {
                crowd.Ask(PlaceholderSound.EnemyDies);
                crowd.Flush(i * 0.01f, V);
            }
            crowd.Ask(PlaceholderSound.EnemyDies);
            Assert.That(crowd.Flush(0.5f, V), Is.Empty, "the seventh within the second is dropped");
            crowd.Ask(PlaceholderSound.EnemyDies);
            Assert.That(crowd.Flush(1.001f, V).Count, Is.EqualTo(1), "the first has left the second: one plays");
            crowd.Clear();
            crowd.Ask(PlaceholderSound.EnemyDies);
            Assert.That(crowd.Flush(1.002f, V).Count, Is.EqualTo(1));
        }

        // ---------------- Small differences ----------------

        [Test]
        public void ThePitch_StaysInItsRange_AndIsTheSameForTheSameSeed()
        {
            var a = new PitchVariation(21u);
            var b = new PitchVariation(21u);
            var c = new PitchVariation(22u);
            var seen = new List<float>();
            bool differs = false;
            for (int i = 0; i < 10000; i++)
            {
                float p = a.Next(0.04f);
                Assert.That(p, Is.InRange(0.96f, 1.04f));
                Assert.That(b.Next(0.04f), Is.EqualTo(p), "the same seed, the same pitches");
                differs |= Math.Abs(c.Next(0.04f) - p) > 1e-6f;
                seen.Add(p);
            }
            Assert.That(differs, Is.True, "another seed, others");
            Assert.That(seen.Max() - seen.Min(), Is.GreaterThan(0.07f), "it uses its range");
            Assert.That(seen.Distinct().Count(), Is.GreaterThan(9000), "they differ from play to play");
            Assert.That(new PitchVariation(5u).Next(0f), Is.EqualTo(1f), "no spread: as recorded");
            Assert.That(V.PitchSpread, Is.InRange(0.02f, 0.06f), "a few percent");
        }

        // ---------------- Which moment gives which sound ----------------

        [TestCase(SoundMoment.EnemyHit, EnemyKind.Swarmer, false, PlaceholderSound.EnemyHit)]
        [TestCase(SoundMoment.EnemyDied, EnemyKind.Guard, false, PlaceholderSound.EnemyDies)]
        [TestCase(SoundMoment.WindupStarted, EnemyKind.Swarmer, false, PlaceholderSound.WindupRed)]
        [TestCase(SoundMoment.WindupStarted, EnemyKind.Brute, true, PlaceholderSound.WindupBlue)]
        [TestCase(SoundMoment.WindupStarted, EnemyKind.Boss, true, PlaceholderSound.WindupBlue)]
        [TestCase(SoundMoment.WindupStarted, EnemyKind.Shooter, false, PlaceholderSound.WindupRed)]
        [TestCase(SoundMoment.EnemyStruck, EnemyKind.Brute, false, PlaceholderSound.Strike)]
        [TestCase(SoundMoment.EnemyStruck, EnemyKind.Boss, false, PlaceholderSound.Strike)]
        [TestCase(SoundMoment.EnemyStruck, EnemyKind.Shooter, false, PlaceholderSound.Arrow)]
        [TestCase(SoundMoment.EnemyDazed, EnemyKind.Guard, false, PlaceholderSound.Dazed)]
        [TestCase(SoundMoment.BossCharged, EnemyKind.Boss, false, PlaceholderSound.BossCharge)]
        [TestCase(SoundMoment.BossAngered, EnemyKind.Boss, false, PlaceholderSound.BossAnger)]
        [TestCase(SoundMoment.BossCalled, EnemyKind.Boss, false, PlaceholderSound.BossCall)]
        [TestCase(SoundMoment.HerHurt, EnemyKind.Brute, false, PlaceholderSound.Hurt)]
        [TestCase(SoundMoment.HerDied, EnemyKind.Brute, false, PlaceholderSound.HerDeath)]
        [TestCase(SoundMoment.Dashed, EnemyKind.Brute, false, PlaceholderSound.Dash)]
        [TestCase(SoundMoment.ChestOpened, EnemyKind.Brute, false, PlaceholderSound.ChestOpens)]
        [TestCase(SoundMoment.ChoiceTaken, EnemyKind.Brute, false, PlaceholderSound.ChoiceTaken)]
        [TestCase(SoundMoment.FloorCleared, EnemyKind.Brute, false, PlaceholderSound.FloorCleared)]
        [TestCase(SoundMoment.ActCleared, EnemyKind.Brute, false, PlaceholderSound.ActCleared)]
        [TestCase(SoundMoment.MenuOpened, EnemyKind.Brute, false, PlaceholderSound.Menu)]
        [TestCase(SoundMoment.MenuClosed, EnemyKind.Brute, false, PlaceholderSound.Menu)]
        [TestCase(SoundMoment.MenuMoved, EnemyKind.Brute, false, PlaceholderSound.Menu)]
        [TestCase(SoundMoment.FocusFilled, EnemyKind.Brute, false, PlaceholderSound.FocusFull)]
        public void EachMoment_GivesItsSound(SoundMoment moment, EnemyKind kind, bool counterable, PlaceholderSound sound)
        {
            Assert.That(PlaceholderSoundRules.SoundFor(moment, kind, counterable), Is.EqualTo(sound));
        }

        [Test]
        public void EveryMoment_GivesASound_AndEverySoundHasAMoment()
        {
            var given = new HashSet<PlaceholderSound>();
            foreach (SoundMoment m in Enum.GetValues(typeof(SoundMoment)))
                foreach (bool counterable in new[] { false, true })
                    foreach (EnemyKind k in Enum.GetValues(typeof(EnemyKind)))
                    {
                        PlaceholderSound? s = PlaceholderSoundRules.SoundFor(m, k, counterable);
                        Assert.That(s.HasValue, Is.True, m.ToString());
                        given.Add(s.Value);
                    }
            Assert.That(given, Is.EquivalentTo(PlaceholderSoundRules.All));
        }

        // ---------------- Paused is silent ----------------

        [TestCaseSource(nameof(Sounds))]
        public void WhilePaused_OnlyTheMenuTickAndTheChoiceChord_MayStart(PlaceholderSound sound)
        {
            bool allowed = sound == PlaceholderSound.Menu || sound == PlaceholderSound.ChoiceTaken;
            Assert.That(PlaceholderSoundRules.MayStart(sound, paused: true), Is.EqualTo(allowed));
            Assert.That(PlaceholderSoundRules.MayStart(sound, paused: false), Is.True);
        }

        // ---------------- Quiet beside the guns ----------------

        [TestCaseSource(nameof(Sounds))]
        public void EachVolume_IsBetweenAFifthAndAHalfOfTheBasicShot(PlaceholderSound sound)
        {
            Assert.That(V.Volume(sound), Is.InRange(0.2f, 0.5f));
            Assert.That(V.PerSecond(sound), Is.GreaterThan(0));
        }

        [Test]
        public void TheHitTick_IsTheLowest_TheBossAndHerPain_TheHighest()
        {
            float lowest = PlaceholderSoundRules.All.Min(s => V.Volume(s));
            float highest = PlaceholderSoundRules.All.Max(s => V.Volume(s));
            Assert.That(V.Volume(PlaceholderSound.EnemyHit), Is.EqualTo(lowest));
            foreach (PlaceholderSound s in new[] { PlaceholderSound.BossCharge, PlaceholderSound.BossAnger, PlaceholderSound.BossCall, PlaceholderSound.Hurt })
                Assert.That(V.Volume(s), Is.EqualTo(highest), s.ToString());
            Assert.That(V.Sounds.Select(s => s.Sound).Distinct().Count(), Is.EqualTo(PlaceholderSoundRules.All.Length), "every sound has its line, once");
            Assert.That(V.On, Is.True);
            Assert.That(V.Master, Is.EqualTo(1f));
        }

        [Test]
        public void HerHurt_IsLouderForABiggerHit_UpToItsFullVolume()
        {
            float small = PlaceholderSoundRules.HurtGain(1, 100, V);
            float mid = PlaceholderSoundRules.HurtGain(12, 100, V);
            float big = PlaceholderSoundRules.HurtGain(30, 100, V);
            Assert.That(small, Is.GreaterThanOrEqualTo(V.HurtSmallest));
            Assert.That(mid, Is.GreaterThan(small));
            Assert.That(big, Is.GreaterThan(mid));
            Assert.That(big, Is.EqualTo(1f), "a quarter of her health or more: full");
            Assert.That(PlaceholderSoundRules.HurtGain(1000, 100, V), Is.EqualTo(1f));
            Assert.That(PlaceholderSoundRules.HurtGain(0, 100, V), Is.EqualTo(V.HurtSmallest));
        }

        [Test]
        public void ARecordedClip_IsLookedForUnderItsName()
        {
            Assert.That(PlaceholderSoundRules.ResourcePath(PlaceholderSound.EnemyHit), Is.EqualTo("Sounds/Placeholders/EnemyHit"));
            Assert.That(PlaceholderSoundRules.ResourcePath(PlaceholderSound.WindupBlue), Is.EqualTo("Sounds/Placeholders/WindupBlue"));
        }
    }
}
