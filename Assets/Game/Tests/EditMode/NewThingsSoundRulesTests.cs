using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 048 (Kristian's wish of 2026-11-20 for sounds for the new things), as rules: each of the ten is in the table
    /// with a volume and a cap, quiet beside the guns; each moment picks its sound; the crit's tick and a clean press's own
    /// tick are never both chosen; the best card's tier picks the offer's bells; each clip has samples of its length, starts
    /// and ends in silence, never clips, and differs from every other. Nobody can hear here whether they sound right.
    /// </summary>
    public class NewThingsSoundRulesTests
    {
        static readonly PlaceholderSoundValues V = new PlaceholderSoundValues();

        /// <summary>The ten, with the length each was given.</summary>
        static readonly Dictionary<PlaceholderSound, float> New = new Dictionary<PlaceholderSound, float>
        {
            { PlaceholderSound.ProjectileStopped, 0.08f }, { PlaceholderSound.OrbLoosed, 0.18f }, { PlaceholderSound.Duck, 0.12f },
            { PlaceholderSound.Stunned, 0.45f }, { PlaceholderSound.Pulled, 0.18f }, { PlaceholderSound.DropLands, 0.35f },
            { PlaceholderSound.DropTaken, 0.2f }, { PlaceholderSound.WayOpens, 0.6f }, { PlaceholderSound.Crit, 0.06f },
            { PlaceholderSound.RareOffer, 0.5f },
        };

        static IEnumerable<PlaceholderSound> NewSounds() => New.Keys;

        [Test]
        public void TheTen_AreSounds_AfterTheNineteen_WhichKeepTheirPlaces()
        {
            Assert.That(PlaceholderSoundRules.All.Length, Is.EqualTo(41)); // changed deliberately (work order 080): its nine after these; (work order 108) the lancer's three after those
            Assert.That((int)PlaceholderSound.FocusFull, Is.EqualTo(18), "the nineteen where they were");
            foreach (PlaceholderSound s in New.Keys) Assert.That((int)s, Is.GreaterThan(18), s.ToString());
        }

        // ---------------- The table ----------------

        [TestCaseSource(nameof(NewSounds))]
        public void EachIsInTheTable_WithAVolumeAndACap_QuietBesideTheGuns(PlaceholderSound sound)
        {
            Assert.That(V.Sounds.Count(s => s.Sound == sound), Is.EqualTo(1), "its own line, once");
            Assert.That(V.Volume(sound), Is.InRange(0.2f, 0.5f), "between a fifth and a half of the basic shot");
            Assert.That(V.Volume(sound), Is.GreaterThanOrEqualTo(V.Volume(PlaceholderSound.EnemyHit)), "no quieter than the hit tick");
            Assert.That(V.Volume(sound), Is.LessThanOrEqualTo(V.Volume(PlaceholderSound.Hurt)), "no louder than her pain");
            Assert.That(V.PerSecond(sound), Is.InRange(1, 12));
        }

        [Test]
        public void TheKnock_IsTheQuietest_AndCappedHard_TheCrit_CappedLikeTheHitTick()
        {
            Assert.That(V.Volume(PlaceholderSound.ProjectileStopped), Is.EqualTo(V.Volume(PlaceholderSound.EnemyHit)), "quiet");
            Assert.That(V.PerSecond(PlaceholderSound.ProjectileStopped), Is.LessThanOrEqualTo(4));
            Assert.That(V.StoppedGap, Is.GreaterThanOrEqualTo(0.2f), "a fan's five, all within a fifth of a second, are one");
            Assert.That(V.PerSecond(PlaceholderSound.Crit), Is.EqualTo(V.PerSecond(PlaceholderSound.EnemyHit)));
            Assert.That(V.Volume(PlaceholderSound.OrbLoosed), Is.LessThan(V.Volume(PlaceholderSound.Arrow)), "the orb softer than the arrow");
        }

        [Test]
        public void AFanOfFive_IntoAWall_IsOneKnock_AndTheNextVolley_ItsOwn()
        {
            var gate = new RapidStrikeGate();
            int heard = 0;
            for (int i = 0; i < 5; i++)
                if (gate.Ask(1f + i * 0.03f, V.StoppedGap)) heard++; // five, a frame or two apart
            Assert.That(heard, Is.EqualTo(1));
            Assert.That(gate.Ask(2f, V.StoppedGap), Is.True, "a volley a second later knocks again");
        }

        // ---------------- Which moment gives which sound ----------------

        [TestCase(SoundMoment.ProjectileStopped, EnemyKind.Shooter, PlaceholderSound.ProjectileStopped)]
        [TestCase(SoundMoment.ProjectileStopped, EnemyKind.Caster, PlaceholderSound.ProjectileStopped)]
        [TestCase(SoundMoment.ProjectileStopped, EnemyKind.Boss, PlaceholderSound.ProjectileStopped)]
        [TestCase(SoundMoment.Loosed, EnemyKind.Caster, PlaceholderSound.OrbLoosed)]
        [TestCase(SoundMoment.Loosed, EnemyKind.Shooter, PlaceholderSound.Arrow)]
        [TestCase(SoundMoment.Loosed, EnemyKind.Boss, PlaceholderSound.Strike)]
        [TestCase(SoundMoment.Ducked, EnemyKind.Brute, PlaceholderSound.Duck)]
        [TestCase(SoundMoment.HerStunned, EnemyKind.Brute, PlaceholderSound.Stunned)]
        [TestCase(SoundMoment.HerPulled, EnemyKind.Guard, PlaceholderSound.Pulled)]
        [TestCase(SoundMoment.DropLanded, EnemyKind.Brute, PlaceholderSound.DropLands)]
        [TestCase(SoundMoment.DropTaken, EnemyKind.Brute, PlaceholderSound.DropTaken)]
        [TestCase(SoundMoment.CombatRoomCleared, EnemyKind.Brute, PlaceholderSound.WayOpens)]
        [TestCase(SoundMoment.HerCrit, EnemyKind.Brute, PlaceholderSound.Crit)]
        [TestCase(SoundMoment.RareOffered, EnemyKind.Brute, PlaceholderSound.RareOffer)]
        public void EachNewMoment_GivesItsSound(SoundMoment moment, EnemyKind kind, PlaceholderSound sound)
        {
            Assert.That(PlaceholderSoundRules.SoundFor(moment, kind), Is.EqualTo(sound));
        }

        [Test]
        public void ACastersCircleBursting_IsStillTheStrike_OnlyItsOrbLeaving_IsTheOrbsSound()
        {
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.EnemyStruck, EnemyKind.Caster), Is.EqualTo(PlaceholderSound.Strike));
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.Loosed, EnemyKind.Caster), Is.EqualTo(PlaceholderSound.OrbLoosed));
        }

        [Test]
        public void TheCritsTick_AndACleanPresssOwnTick_AreNeverBothChosen()
        {
            Assert.That(PlaceholderSoundRules.CritSound(ownTickRang: false), Is.EqualTo(PlaceholderSound.Crit), "a rolled crit: its tick");
            Assert.That(PlaceholderSoundRules.CritSound(ownTickRang: true), Is.Null, "a clean press's or a perfect release's: its own tone only");
        }

        [Test]
        public void TheBestCardsTier_PicksTheOffersBells()
        {
            Assert.That(PlaceholderSoundRules.OfferBells(null), Is.EqualTo(0), "no tiers: none");
            Assert.That(PlaceholderSoundRules.OfferBells(UpgradeTier.Common), Is.EqualTo(0), "commons: none");
            Assert.That(PlaceholderSoundRules.OfferBells(UpgradeTier.Rare), Is.EqualTo(1), "a rare: one bell");
            Assert.That(PlaceholderSoundRules.OfferBells(UpgradeTier.Epic), Is.EqualTo(2), "an epic: two");
            Assert.That(PlaceholderSoundRules.EpicNoteStep, Is.EqualTo(4f / 3f).Within(1e-6f), "the second a fourth up: rising");
            Assert.That(V.EpicNoteGap, Is.InRange(0.08f, 0.3f), "a moment later, not over the first's whole ring");
        }

        [TestCaseSource(nameof(NewSounds))]
        public void NoneMayStartWhilePaused(PlaceholderSound sound)
        {
            Assert.That(PlaceholderSoundRules.MayStart(sound, paused: true), Is.False);
            Assert.That(PlaceholderSoundRules.MayStart(sound, paused: false), Is.True);
        }

        // ---------------- The clips ----------------

        [TestCaseSource(nameof(NewSounds))]
        public void EachClip_HasSamplesOfItsLength_FromAndToSilence_NeverClipping(PlaceholderSound sound)
        {
            float[] s = Synth.Render(PlaceholderRecipes.For(sound), Synth.SampleRate);
            Assert.That(PlaceholderRecipes.Seconds(sound), Is.EqualTo(New[sound]).Within(1e-6f), "its length");
            Assert.That(s.Length, Is.EqualTo((int)Math.Round(New[sound] * Synth.SampleRate)));
            Assert.That(s.Any(v => float.IsNaN(v) || float.IsInfinity(v)), Is.False, "no NaN");
            float peak = s.Max(v => Math.Abs(v));
            Assert.That(peak, Is.LessThan(1f), "no clipping");
            Assert.That(peak, Is.LessThanOrEqualTo(0.9f + 1e-4f));
            Assert.That(peak, Is.GreaterThan(0.3f), "not silent");
            Assert.That(s[0], Is.EqualTo(0f));
            Assert.That(s[s.Length - 1], Is.EqualTo(0f));
            Assert.That(Synth.Render(PlaceholderRecipes.For(sound)), Is.EqualTo(s), "the same when made twice");
        }

        [Test]
        public void TheDuck_IsNotTheDash_TheCrit_NotTheHitTick_AndTheOrb_LowerThanTheArrow()
        {
            float[] duck = Synth.Render(PlaceholderRecipes.For(PlaceholderSound.Duck));
            float[] dash = Synth.Render(PlaceholderRecipes.For(PlaceholderSound.Dash));
            Assert.That(duck.Length, Is.LessThan(dash.Length), "shorter");
            Assert.That(Crossings(duck) / (float)duck.Length, Is.LessThan(0.5f * Crossings(dash) / dash.Length), "a low thud, not a bright whoosh");

            float[] crit = Synth.Render(PlaceholderRecipes.For(PlaceholderSound.Crit));
            float[] hit = Synth.Render(PlaceholderRecipes.For(PlaceholderSound.EnemyHit));
            Assert.That(Crossings(crit) / (float)crit.Length, Is.GreaterThan(3f * Crossings(hit) / hit.Length), "bright over the dull tick");

            // The orb's tone under the arrow's (by the recipes: dull noise counts no fewer zero crossings than a tone).
            float Highest(PlaceholderSound s) => PlaceholderRecipes.For(s).Layers.Where(l => l.Wave == SynthWave.Sine).Max(l => Math.Max(l.From, l.To));
            float Lowest(PlaceholderSound s) => PlaceholderRecipes.For(s).Layers.Where(l => l.Wave == SynthWave.Sine).Min(l => l.To > 0f ? Math.Min(l.From, l.To) : l.From);
            Assert.That(Highest(PlaceholderSound.OrbLoosed), Is.LessThan(Lowest(PlaceholderSound.Arrow)), "the orb lower than the arrow");
            SynthLayer orbNoise = PlaceholderRecipes.For(PlaceholderSound.OrbLoosed).Layers.First(l => l.Wave == SynthWave.Noise);
            Assert.That(orbNoise.LowPass, Is.LessThan(1000f), "and its breath dull");
        }

        static int Crossings(float[] s)
        {
            int n = 0;
            for (int i = 1; i < s.Length; i++)
                if ((s[i - 1] < 0f) != (s[i] < 0f)) n++;
            return n;
        }
    }
}
